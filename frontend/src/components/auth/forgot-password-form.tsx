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
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-50 px-4 py-10">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -left-36 top-1/4 size-80 rounded-full bg-blue-100/60 blur-3xl" />
        <div className="absolute -right-32 bottom-1/4 size-72 rounded-full bg-amber-100/60 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(to_right,#e2e8f044_1px,transparent_1px),linear-gradient(to_bottom,#e2e8f044_1px,transparent_1px)] bg-[size:32px_32px]" />
      </div>

      <section className="relative w-full max-w-md rounded-2xl border border-slate-200 bg-white p-7 shadow-xl shadow-slate-900/[0.06] sm:p-9">
        <Logo />

        {result ? (
          <div className="mt-8 text-center">
            <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-emerald-50 text-emerald-600">
              <MailCheck className="size-7" />
            </div>
            <h1 className="mt-5 text-2xl font-semibold tracking-tight text-slate-950">
              Kiểm tra hộp thư của bạn
            </h1>
            <p className="mt-3 text-sm leading-6 text-slate-500">
              {result.message}
            </p>

            {result.debugResetUrl ? (
              <div className="mt-6 rounded-xl border border-amber-200 bg-amber-50 p-4">
                <p className="text-xs font-medium uppercase tracking-wide text-amber-700">
                  Recruiter Demo Mode
                </p>
                <Link
                  className="mt-3 inline-flex h-11 w-full items-center justify-center gap-2 rounded-xl bg-amber-500 px-4 text-sm font-semibold text-white transition hover:bg-amber-600"
                  href={result.debugResetUrl}
                >
                  <Zap className="size-4" />
                  Demo: Mở trang đặt lại mật khẩu ngay
                </Link>
                <a
                  className="mt-3 inline-flex items-center gap-1.5 text-xs font-medium text-amber-800 underline decoration-amber-300 underline-offset-2"
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
              <p className="text-sm font-medium text-blue-600">
                Khôi phục tài khoản
              </p>
              <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-slate-950">
                Quên mật khẩu?
              </h1>
              <p className="mt-2 text-sm leading-6 text-slate-500">
                Nhập email của bạn. Nếu tài khoản tồn tại, OmniDoc sẽ gửi một
                liên kết dùng một lần có hiệu lực trong 15 phút.
              </p>
            </div>

            {error ? (
              <div
                className="mt-5 flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 px-3.5 py-3 text-sm text-rose-700"
                role="alert"
              >
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                <span>{error}</span>
              </div>
            ) : null}

            <form className="mt-6" onSubmit={handleSubmit}>
              <label className="block">
                <span className="mb-1.5 block text-sm font-medium text-slate-700">
                  Email
                </span>
                <div className="relative">
                  <Mail className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
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
          className="mt-6 flex items-center justify-center gap-1.5 text-sm font-medium text-slate-500 transition hover:text-blue-600"
          href="/login"
        >
          <ArrowLeft className="size-4" />
          Quay lại đăng nhập
        </Link>
      </section>
    </main>
  );
}
