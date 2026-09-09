"use client";

import {
  AlertCircle,
  Eye,
  EyeOff,
  LockKeyhole,
  Mail,
  UserRound,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Logo } from "@/components/ui/logo";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { getShowcaseWorkspaceId, isShowcaseUser, showcase } from "@/lib/showcase";
import { getErrorMessage } from "@/services/api-client";
import { workspaceService } from "@/services/workspace.service";

type AuthMode = "login" | "register";

interface FormErrors {
  fullName?: string;
  email?: string;
  password?: string;
  confirmPassword?: string;
}

function validate(
  mode: AuthMode,
  values: { fullName: string; email: string; password: string; confirmPassword: string },
) {
  const errors: FormErrors = {};

  if (mode === "register" && !values.fullName.trim()) {
    errors.fullName = "Vui lòng nhập họ và tên.";
  }

  if (!values.email.trim()) {
    errors.email = "Vui lòng nhập email.";
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email)) {
    errors.email = "Email chưa đúng định dạng.";
  }

  if (!values.password) {
    errors.password = "Vui lòng nhập mật khẩu.";
  } else if (mode === "register" && values.password.length < 8) {
    errors.password = "Mật khẩu cần có ít nhất 8 ký tự.";
  }

  if (mode === "register") {
    if (!values.confirmPassword) {
      errors.confirmPassword = "Vui lòng xác nhận mật khẩu.";
    } else if (values.password !== values.confirmPassword) {
      errors.confirmPassword = "Mật khẩu xác nhận không khớp.";
    }
  }

  return errors;
}

export function AuthForm({
  mode,
  redirectTo = "/workspaces",
  highlightShowcase = false,
}: {
  mode: AuthMode;
  redirectTo?: string;
  highlightShowcase?: boolean;
}) {
  const isRegister = mode === "register";
  const { user, isLoading, login, register } = useAuth();
  const router = useRouter();
  const [values, setValues] = useState({
    fullName: "",
    email: "",
    password: "",
    confirmPassword: "",
  });
  const [errors, setErrors] = useState<FormErrors>({});
  const [requestError, setRequestError] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isShowcaseLogging, setIsShowcaseLogging] = useState(false);

  useEffect(() => {
    if (isLoading || !user) return;
    if (isRegister || !isShowcaseUser(user)) {
      router.replace(redirectTo);
      return;
    }

    if (showcase.workspaceId) {
      router.replace(`/workspaces/${encodeURIComponent(showcase.workspaceId)}/chat`);
      return;
    }

    const controller = new AbortController();
    workspaceService.getAll(controller.signal).then((workspaces) => {
      if (controller.signal.aborted) return;
      const workspaceId = getShowcaseWorkspaceId(workspaces);
      router.replace(workspaceId ? `/workspaces/${encodeURIComponent(workspaceId)}/chat` : "/workspaces");
    }).catch(() => {
      if (!controller.signal.aborted) router.replace("/workspaces");
    });
    return () => controller.abort();
  }, [isLoading, isRegister, redirectTo, router, user]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const validationErrors = validate(mode, values);
    setErrors(validationErrors);
    setRequestError(null);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      if (isRegister) {
        await register({
          fullName: values.fullName.trim(),
          email: values.email.trim(),
          password: values.password,
        });
      } else {
        await login({
          email: values.email.trim(),
          password: values.password,
        });
      }

      // The effect above routes using the authenticated user returned by the server.
    } catch (error) {
      setRequestError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-10">
      {/* Subtle dark ambient glow matching landing page palette */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_30%_20%,rgb(0_240_255/8%),transparent_45%),radial-gradient(ellipse_at_75%_70%,rgb(37_99_235/10%),transparent_45%)]"
      />
      <section className="glass-panel relative w-full max-w-md rounded-2xl p-7 sm:p-9">
        <Logo priority />
        <div className="mt-8">
          <p className="text-sm font-medium text-accent">
            {isRegister ? "Bắt đầu với OmniDoc" : "Chào mừng trở lại"}
          </p>
          <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-content">
            {isRegister ? "Tạo tài khoản của bạn" : "Đăng nhập vào tài khoản"}
          </h1>
          <p className="mt-2 text-sm leading-6 text-muted">
            {isRegister
              ? "Tạo workspace và biến tài liệu thành tri thức có thể tìm kiếm."
              : "Tiếp tục quản lý tài liệu và không gian làm việc của bạn."}
          </p>
        </div>

        {!isRegister && showcase.enabled ? (
          <aside
            id="showcase-quick-fill"
            aria-label="Tài khoản trải nghiệm"
            className={`mt-6 rounded-xl border border-line bg-surface-subtle p-4 ${highlightShowcase ? "ring-2 ring-focus-ring ring-offset-2 ring-offset-surface" : ""}`}
          >
            <Button
              className="h-auto min-h-11 w-full py-2"
              disabled={isSubmitting || isShowcaseLogging}
              type="button"
              onClick={async () => {
                setRequestError(null);
                setIsShowcaseLogging(true);
                try {
                  await login({ email: showcase.email, password: showcase.password });
                } catch (error) {
                  setRequestError(getErrorMessage(error));
                } finally {
                  setIsShowcaseLogging(false);
                }
              }}
            >
              {isShowcaseLogging ? <Spinner /> : null}
              {isShowcaseLogging ? "Đang đăng nhập..." : "Sử dụng tài khoản Trải nghiệm"}
            </Button>
            <p className="mt-2 text-xs leading-5 text-muted" role="status">
              Không gian dùng chung. Vui lòng không nhập thông tin cá nhân hoặc nội dung bí mật.
            </p>
          </aside>
        ) : null}

        {requestError ? (
          <div
            className="mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-3.5 py-3 text-sm text-danger"
            role="alert"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            <span>{requestError}</span>
          </div>
        ) : null}

        <form className="mt-6 space-y-4" onSubmit={handleSubmit} noValidate>
          {isRegister ? (
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-content-secondary">
                Họ và tên
              </span>
              <div className="relative">
                <UserRound className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
                <Input
                  autoComplete="name"
                  className="pl-10"
                  error={Boolean(errors.fullName)}
                  maxLength={200}
                  onChange={(event) => {
                    setValues((current) => ({
                      ...current,
                      fullName: event.target.value,
                    }));
                    setErrors((current) => ({
                      ...current,
                      fullName: undefined,
                    }));
                  }}
                  placeholder="Name"
                  value={values.fullName}
                />
              </div>
              {errors.fullName ? (
                <span className="mt-1.5 block text-xs text-danger">
                  {errors.fullName}
                </span>
              ) : null}
            </label>
          ) : null}

          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-content-secondary">
              Email
            </span>
            <div className="relative">
              <Mail className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
              <Input
                autoComplete="email"
                className="pl-10"
                error={Boolean(errors.email)}
                inputMode="email"
                maxLength={320}
                onChange={(event) => {
                  setValues((current) => ({
                    ...current,
                    email: event.target.value,
                  }));
                  setErrors((current) => ({
                    ...current,
                    email: undefined,
                  }));
                }}
                placeholder="Email"
                type="email"
                value={values.email}
              />
            </div>
            {errors.email ? (
              <span className="mt-1.5 block text-xs text-danger">
                {errors.email}
              </span>
            ) : null}
          </label>

          <label className="block">
            <span className="mb-1.5 flex items-center justify-between gap-3 text-sm font-medium text-content-secondary">
              <span>Mật khẩu</span>
              {!isRegister ? (
                <Link
                  className="font-medium text-accent transition-colors hover:text-accent-primary"
                  href="/forgot-password"
                >
                  Quên mật khẩu?
                </Link>
              ) : null}
            </span>
            <div className="relative">
              <LockKeyhole className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
              <Input
                autoComplete={isRegister ? "new-password" : "current-password"}
                className="pl-10 pr-12"
                error={Boolean(errors.password)}
                maxLength={128}
                minLength={isRegister ? 8 : undefined}
                onChange={(event) => {
                  setValues((current) => ({
                    ...current,
                    password: event.target.value,
                  }));
                  setErrors((current) => ({
                    ...current,
                    password: undefined,
                  }));
                }}
                placeholder={isRegister ? "Tối thiểu 8 ký tự" : "Nhập mật khẩu"}
                type={showPassword ? "text" : "password"}
                value={values.password}
              />
              <button
                aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                className="absolute right-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                onClick={() => setShowPassword((current) => !current)}
                type="button"
              >
                {showPassword ? (
                  <EyeOff className="size-4" />
                ) : (
                  <Eye className="size-4" />
                )}
              </button>
            </div>
            {errors.password ? (
              <span className="mt-1.5 block text-xs text-danger">
                {errors.password}
              </span>
            ) : null}
          </label>

          {isRegister ? (
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-content-secondary">
                Xác nhận mật khẩu
              </span>
              <div className="relative">
                <LockKeyhole className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
                <Input
                  autoComplete="new-password"
                  className="pl-10 pr-12"
                  error={Boolean(errors.confirmPassword)}
                  maxLength={128}
                  onChange={(event) => {
                    setValues((current) => ({
                      ...current,
                      confirmPassword: event.target.value,
                    }));
                    setErrors((current) => ({
                      ...current,
                      confirmPassword: undefined,
                    }));
                  }}
                  placeholder="Nhập lại mật khẩu"
                  type={showConfirmPassword ? "text" : "password"}
                  value={values.confirmPassword}
                />
                <button
                  aria-label={showConfirmPassword ? "Ẩn mật khẩu xác nhận" : "Hiện mật khẩu xác nhận"}
                  className="absolute right-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                  onClick={() => setShowConfirmPassword((current) => !current)}
                  type="button"
                >
                  {showConfirmPassword ? (
                    <EyeOff className="size-4" />
                  ) : (
                    <Eye className="size-4" />
                  )}
                </button>
              </div>
              {errors.confirmPassword ? (
                <span className="mt-1.5 block text-xs text-danger">
                  {errors.confirmPassword}
                </span>
              ) : null}
            </label>
          ) : null}

          <Button
            className="mt-2 w-full"
            disabled={isSubmitting}
            size="lg"
            type="submit"
          >
            {isSubmitting ? <Spinner /> : null}
            {isSubmitting
              ? isRegister
                ? "Đang tạo tài khoản..."
                : "Đang đăng nhập..."
              : isRegister
                ? "Tạo tài khoản"
                : "Đăng nhập"}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-muted">
          {isRegister ? "Đã có tài khoản?" : "Chưa có tài khoản?"}{" "}
          <Link
            className="font-medium text-accent transition-colors hover:text-accent-primary"
            href={`${isRegister ? "/login" : "/register"}?returnUrl=${encodeURIComponent(redirectTo)}`}
          >
            {isRegister ? "Đăng nhập" : "Đăng ký ngay"}
          </Link>
        </p>
      </section>
    </main>
  );
}
