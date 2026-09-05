"use client";

import {
  AlertCircle,
  ArrowLeft,
  ExternalLink,
  Mail,
  MailCheck,
  Send,
  Zap,
} from "lucide-react";
import Link from "next/link";
import { useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Logo } from "@/components/ui/logo";
import { Spinner } from "@/components/ui/spinner";
import { getErrorMessage } from "@/services/api-client";
import { authService } from "@/services/auth.service";
import type { ForgotPasswordResponse } from "@/types/auth.types";

export function ForgotPasswordForm() {
  const [email, setEmail] = useState("");
  const [result, setResult] = useState<ForgotPasswordResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      setResult(await authService.forgotPassword({ email: email.trim() }));
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="ambient-bg relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-10">
      <section className="glass-panel relative w-full max-w-md rounded-2xl p-7 sm:p-9">
        <Logo priority />

        {result ? (
          <div className="mt-8 text-center">
            <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-success-subtle text-success">
              <MailCheck className="size-7" />
            </div>
            <h1 className="mt-5 text-2xl font-semibold tracking-tight text-content">
              Kiểm tra hộp thư của bạn
            </h1>
            <p className="mt-3 text-sm leading-6 text-muted">
              {result.message}
            </p>

            {result.debugResetUrl ? (
              <div className="mt-6 rounded-xl border border-warning bg-warning-subtle p-4">
                <p className="text-xs font-medium uppercase tracking-wide text-warning">
                  Recruiter Demo Mode
                </p>
                <Link
                  className="mt-3 inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl border border-warning px-4 text-sm font-semibold text-warning transition-[filter,box-shadow] hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                  href={result.debugResetUrl}
                >
                  <Zap className="size-4" />
                  Demo: Mở trang đặt lại mật khẩu ngay
                </Link>
                <a
                  className="mt-3 inline-flex min-h-11 items-center gap-1.5 text-xs font-medium text-warning underline decoration-current underline-offset-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                  href="http://localhost:8025"
                  rel="noreferrer"
                  target="_blank"
                >
                  Mở hộp thư Mailpit
                  <ExternalLink className="size-3" />
                </a>
              </div>
            ) : null}

            <Button
              className="mt-6 w-full"
              onClick={() => setResult(null)}
              variant="secondary"
            >
              Gửi lại hoặc dùng email khác
            </Button>
          </div>
        ) : (
          <>
            <div className="mt-8">
              <p className="text-sm font-medium text-accent">
                Khôi phục tài khoản
              </p>
              <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-content">
                Quên mật khẩu?
              </h1>
              <p className="mt-2 text-sm leading-6 text-muted">
                Nhập email của bạn. Nếu tài khoản tồn tại, OmniDoc sẽ gửi một
                liên kết dùng một lần có hiệu lực trong 15 phút.
              </p>
            </div>

            {error ? (
              <div
                className="mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-3.5 py-3 text-sm text-danger"
                role="alert"
              >
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                <span>{error}</span>
              </div>
            ) : null}

            <form className="mt-6" onSubmit={handleSubmit}>
              <label className="block">
                <span className="mb-1.5 block text-sm font-medium text-content-secondary">
                  Email
                </span>
                <div className="relative">
                  <Mail className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" />
                  <Input
                    autoComplete="email"
                    autoFocus
                    className="pl-10"
                    onChange={(event) => setEmail(event.target.value)}
                    placeholder="you@company.com"
                    required
                    type="email"
                    value={email}
                  />
                </div>
              </label>

              <Button
                className="mt-5 w-full"
                disabled={isSubmitting || !email.trim()}
                size="lg"
                type="submit"
              >
                {isSubmitting ? <Spinner /> : <Send className="size-5" />}
                {isSubmitting ? "Đang gửi..." : "Gửi liên kết đặt lại"}
              </Button>
            </form>
          </>
        )}

        <Link
          className="mt-6 flex min-h-11 items-center justify-center gap-1.5 text-sm font-medium text-muted transition-colors hover:text-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
          href="/login"
        >
          <ArrowLeft className="size-4" />
          Quay lại đăng nhập
        </Link>
      </section>
    </main>
  );
}
