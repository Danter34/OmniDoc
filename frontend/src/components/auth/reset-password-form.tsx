"use client";

import {
  AlertCircle,
  CheckCircle2,
  Eye,
  EyeOff,
  KeyRound,
  LockKeyhole,
} from "lucide-react";
import Link from "next/link";
import { useState, type FormEvent } from "react";

import { PasswordStrength } from "@/components/auth/password-strength";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Logo } from "@/components/ui/logo";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { getErrorMessage } from "@/services/api-client";
import { authService } from "@/services/auth.service";

export function ResetPasswordForm({
  email,
  token,
}: {
  email: string;
  token: string;
}) {
  const { logout } = useAuth();
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isComplete, setIsComplete] = useState(false);
  const hasResetCredentials = Boolean(email && token);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (newPassword.length < 8) {
      setError("Mật khẩu mới cần có ít nhất 8 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await authService.resetPassword({ email, token, newPassword });
      logout();
      setIsComplete(true);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="ambient-bg relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-10">
      <section className="glass-panel relative w-full max-w-md rounded-2xl p-7 sm:p-9">
        <Logo />

        {isComplete ? (
          <div className="mt-8 text-center">
            <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-success-subtle text-success">
              <CheckCircle2 className="size-8" />
            </div>
            <h1 className="mt-5 text-2xl font-semibold text-content">
              Đặt lại mật khẩu thành công
            </h1>
            <p className="mt-2 text-sm leading-6 text-muted">
              Tất cả phiên đăng nhập cũ đã được thu hồi. Hãy đăng nhập lại bằng
              mật khẩu mới của bạn.
            </p>
            <Link
              className="mt-6 inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl px-5 text-base font-medium text-on-accent shadow-sm transition-[filter,box-shadow] hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 [background-image:var(--gradient-action)]"
              href="/login"
            >
              Đi tới đăng nhập
            </Link>
          </div>
        ) : (
          <>
            <div className="mt-8">
              <p className="text-sm font-medium text-accent">
                Bảo mật tài khoản
              </p>
              <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-content">
                Tạo mật khẩu mới
              </h1>
              <p className="mt-2 text-sm leading-6 text-muted">
                Liên kết này chỉ dùng được một lần. Mật khẩu mới sẽ đăng xuất
                mọi phiên OmniDoc đang hoạt động.
              </p>
            </div>

            {!hasResetCredentials ? (
              <div
                className="mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger"
                role="alert"
              >
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                <span>
                  Liên kết đặt lại không đầy đủ. Vui lòng yêu cầu một liên kết
                  mới.
                </span>
              </div>
            ) : null}

            {error ? (
              <div
                className="mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger"
                role="alert"
              >
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                <span>{error}</span>
              </div>
            ) : null}

            {hasResetCredentials ? (
              <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-content-secondary">
                    Mật khẩu mới
                  </span>
                  <div className="relative">
                    <LockKeyhole className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
                    <Input
                      autoComplete="new-password"
                      autoFocus
                      className="pl-10 pr-12"
                      maxLength={128}
                      minLength={8}
                      onChange={(event) => {
                        setNewPassword(event.target.value);
                        setError(null);
                      }}
                      placeholder="Tối thiểu 8 ký tự"
                      required
                      type={showNewPassword ? "text" : "password"}
                      value={newPassword}
                    />
                    <button
                      aria-label={showNewPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                      className="absolute right-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                      onClick={() => setShowNewPassword((current) => !current)}
                      type="button"
                    >
                      {showNewPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                    </button>
                  </div>
                  <PasswordStrength password={newPassword} />
                </label>

                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-content-secondary">
                    Xác nhận mật khẩu mới
                  </span>
                  <div className="relative">
                    <KeyRound className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
                    <Input
                      autoComplete="new-password"
                      className="pl-10 pr-12"
                      error={Boolean(confirmPassword && confirmPassword !== newPassword)}
                      maxLength={128}
                      minLength={8}
                      onChange={(event) => {
                        setConfirmPassword(event.target.value);
                        setError(null);
                      }}
                      required
                      type={showConfirmation ? "text" : "password"}
                      value={confirmPassword}
                    />
                    <button
                      aria-label={showConfirmation ? "Ẩn mật khẩu xác nhận" : "Hiện mật khẩu xác nhận"}
                      className="absolute right-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                      onClick={() => setShowConfirmation((current) => !current)}
                      type="button"
                    >
                      {showConfirmation ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                    </button>
                  </div>
                </label>

                <Button
                  className="w-full"
                  disabled={isSubmitting || !newPassword || !confirmPassword}
                  size="lg"
                  type="submit"
                >
                  {isSubmitting ? <Spinner /> : <KeyRound className="size-5" />}
                  {isSubmitting ? "Đang cập nhật..." : "Đặt lại mật khẩu"}
                </Button>
              </form>
            ) : (
              <Link
                className="mt-6 inline-flex h-11 w-full items-center justify-center rounded-xl px-4 text-sm font-medium text-on-accent transition-[filter,box-shadow] hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring [background-image:var(--gradient-action)]"
                href="/forgot-password"
              >
                Yêu cầu liên kết mới
              </Link>
            )}
          </>
        )}
      </section>
    </main>
  );
}
