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
import { getErrorMessage } from "@/services/api-client";

type AuthMode = "login" | "register";

interface FormErrors {
  fullName?: string;
  email?: string;
  password?: string;
}

function validate(
  mode: AuthMode,
  values: { fullName: string; email: string; password: string },
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

  return errors;
}

export function AuthForm({
  mode,
  redirectTo = "/workspaces",
}: {
  mode: AuthMode;
  redirectTo?: string;
}) {
  const isRegister = mode === "register";
  const { user, isLoading, login, register } = useAuth();
  const router = useRouter();
  const [values, setValues] = useState({
    fullName: "",
    email: "",
    password: "",
  });
  const [errors, setErrors] = useState<FormErrors>({});
  const [requestError, setRequestError] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!isLoading && user) {
      router.replace(redirectTo);
    }
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

      router.replace(redirectTo);
    } catch (error) {
      setRequestError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-50 px-4 py-10">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -left-36 top-1/4 size-80 rounded-full bg-blue-100/60 blur-3xl" />
        <div className="absolute -right-32 bottom-1/4 size-72 rounded-full bg-amber-100/60 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(to_right,#e2e8f044_1px,transparent_1px),linear-gradient(to_bottom,#e2e8f044_1px,transparent_1px)] bg-[size:32px_32px]" />
      </div>

      <section className="relative w-full max-w-md rounded-2xl border border-slate-200 bg-white p-7 shadow-xl shadow-slate-900/[0.06] sm:p-9">
        <Logo />
        <div className="mt-8">
          <p className="text-sm font-medium text-blue-600">
            {isRegister ? "Bắt đầu với OmniDoc" : "Chào mừng trở lại"}
          </p>
          <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-slate-950">
            {isRegister ? "Tạo tài khoản của bạn" : "Đăng nhập vào tài khoản"}
          </h1>
          <p className="mt-2 text-sm leading-6 text-slate-500">
            {isRegister
              ? "Tạo workspace và biến tài liệu PDF thành tri thức có thể tìm kiếm."
              : "Tiếp tục quản lý tài liệu và không gian làm việc của bạn."}
          </p>
        </div>

        {requestError ? (
          <div
            className="mt-5 flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-sm text-rose-700"
            role="alert"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            <span>{requestError}</span>
          </div>
        ) : null}

        <form className="mt-6 space-y-4" onSubmit={handleSubmit} noValidate>
          {isRegister ? (
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-slate-700">
                Họ và tên
              </span>
              <div className="relative">
                <UserRound className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
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
                  placeholder="Nguyễn Văn An"
                  value={values.fullName}
                />
              </div>
              {errors.fullName ? (
                <span className="mt-1.5 block text-xs text-rose-600">
                  {errors.fullName}
                </span>
              ) : null}
            </label>
          ) : null}

          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">
              Email
            </span>
            <div className="relative">
              <Mail className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
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
                placeholder="you@company.com"
                type="email"
                value={values.email}
              />
            </div>
            {errors.email ? (
              <span className="mt-1.5 block text-xs text-rose-600">
                {errors.email}
              </span>
            ) : null}
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">
              Mật khẩu
            </span>
            <div className="relative">
              <LockKeyhole className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
              <Input
                autoComplete={isRegister ? "new-password" : "current-password"}
                className="px-10"
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
                className="absolute right-2.5 top-1/2 flex size-8 -translate-y-1/2 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
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
              <span className="mt-1.5 block text-xs text-rose-600">
                {errors.password}
              </span>
            ) : null}
          </label>

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

        <p className="mt-6 text-center text-sm text-slate-500">
          {isRegister ? "Đã có tài khoản?" : "Chưa có tài khoản?"}{" "}
          <Link
            className="font-medium text-blue-600 transition hover:text-blue-700"
            href={`${isRegister ? "/login" : "/register"}?redirect=${encodeURIComponent(redirectTo)}`}
          >
            {isRegister ? "Đăng nhập" : "Đăng ký ngay"}
          </Link>
        </p>
      </section>
    </main>
  );
}
