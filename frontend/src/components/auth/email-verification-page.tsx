"use client";

import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  MailCheck,
  RotateCcw,
} from "lucide-react";
import { useRouter } from "next/navigation";
import {
  useEffect,
  useRef,
  useState,
  type ClipboardEvent,
  type FormEvent,
  type KeyboardEvent,
} from "react";

import { Button } from "@/components/ui/button";
import { Logo } from "@/components/ui/logo";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import { authService } from "@/services/auth.service";

const OTP_LENGTH = 6;

function secondsUntil(value?: string | null) {
  if (!value) {
    return 0;
  }

  return Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000));
}

export function EmailVerificationPage({ redirectTo }: { redirectTo: string }) {
  const router = useRouter();
  const { user, refreshUser } = useAuth();
  const inputRefs = useRef<Array<HTMLInputElement | null>>([]);
  const [digits, setDigits] = useState<string[]>(() => Array(OTP_LENGTH).fill(""));
  const [countdown, setCountdown] = useState(() =>
    secondsUntil(user?.otpResendAvailableAt),
  );
  const [error, setError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [isVerified, setIsVerified] = useState(Boolean(user?.emailConfirmed));
  const [hasOtpBeenSent, setHasOtpBeenSent] = useState(
    Boolean(user?.otpResendAvailableAt),
  );

  useEffect(() => {
    if (countdown <= 0) {
      return;
    }

    const timer = window.setInterval(() => {
      setCountdown((current) => Math.max(0, current - 1));
    }, 1000);

    return () => window.clearInterval(timer);
  }, [countdown]);

  useEffect(() => {
    if (!isVerified) {
      return;
    }

    const timer = window.setTimeout(() => {
      router.replace(redirectTo);
    }, 1200);

    return () => window.clearTimeout(timer);
  }, [isVerified, redirectTo, router]);

  function focusInput(index: number) {
    inputRefs.current[index]?.focus();
    inputRefs.current[index]?.select();
  }

  function updateDigit(index: number, value: string) {
    const numericValue = value.replace(/\D/g, "");

    if (numericValue.length > 1) {
      applyPastedOtp(numericValue);
      return;
    }

    setDigits((current) => {
      const next = [...current];
      next[index] = numericValue;
      return next;
    });

    if (numericValue && index < OTP_LENGTH - 1) {
      focusInput(index + 1);
    }
  }

  function applyPastedOtp(value: string) {
    const pastedDigits = value.replace(/\D/g, "").slice(0, OTP_LENGTH);
    if (!pastedDigits) {
      return;
    }

    const next = Array(OTP_LENGTH).fill("") as string[];
    pastedDigits.split("").forEach((digit, index) => {
      next[index] = digit;
    });
    setDigits(next);
    focusInput(Math.min(pastedDigits.length, OTP_LENGTH - 1));
  }

  function handlePaste(event: ClipboardEvent<HTMLElement>) {
    event.preventDefault();
    applyPastedOtp(event.clipboardData.getData("text"));
  }

  function handleKeyDown(index: number, event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Backspace") {
      event.preventDefault();
      const targetIndex = digits[index] ? index : Math.max(0, index - 1);
      setDigits((current) => {
        const next = [...current];
        next[targetIndex] = "";
        return next;
      });
      focusInput(Math.max(0, index - 1));
    } else if (event.key === "ArrowLeft" && index > 0) {
      event.preventDefault();
      focusInput(index - 1);
    } else if (event.key === "ArrowRight" && index < OTP_LENGTH - 1) {
      event.preventDefault();
      focusInput(index + 1);
    }
  }

  async function verifyOtp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const otp = digits.join("");

    if (otp.length !== OTP_LENGTH) {
      setError("Vui lòng nhập đủ 6 chữ số.");
      return;
    }

    setIsVerifying(true);
    setError(null);

    try {
      await authService.verifyEmail(otp);
      await refreshUser();
      setIsVerified(true);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      setDigits(Array(OTP_LENGTH).fill(""));
      focusInput(0);
    } finally {
      setIsVerifying(false);
    }
  }

  async function resendOtp() {
    setIsResending(true);
    setError(null);

    try {
      const response = await authService.sendVerificationOtp();
      setCountdown(secondsUntil(response.resendAvailableAt));
      setHasOtpBeenSent(true);
      setDigits(Array(OTP_LENGTH).fill(""));
      focusInput(0);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsResending(false);
    }
  }

  if (isVerified) {
    return (
      <VerificationShell>
        <div className="text-center">
          <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-emerald-50 text-emerald-600">
            <CheckCircle2 className="size-8" />
          </div>
          <h1 className="mt-5 text-2xl font-semibold text-slate-950">Email đã được xác minh</h1>
          <p className="mt-2 text-sm leading-6 text-slate-500">Tài khoản của bạn đã sẵn sàng. Đang chuyển về workspace...</p>
          <Button className="mt-6 w-full" onClick={() => router.replace(redirectTo)} size="lg">
            Tiếp tục <ArrowRight className="size-4" />
          </Button>
        </div>
      </VerificationShell>
    );
  }

  return (
    <VerificationShell>
      <div className="text-center">
        <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-blue-50 text-blue-600">
          <MailCheck className="size-7" />
        </div>
        <p className="mt-5 text-sm font-medium text-blue-600">Xác minh tài khoản</p>
        <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-slate-950">Nhập mã xác minh</h1>
        <p className="mt-3 text-sm leading-6 text-slate-500">
          {hasOtpBeenSent ? "Mã OTP 6 số đã được gửi tới " : "Gửi mã OTP 6 số tới "}
          <strong className="font-medium text-slate-700">{user?.email}</strong>.
          {hasOtpBeenSent ? " Mã có hiệu lực trong 10 phút." : " để bắt đầu xác minh."}
        </p>
      </div>

      {error ? (
        <div className="mt-5 flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700" role="alert">
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
          <span>{error}</span>
        </div>
      ) : null}

      <form className="mt-7" onSubmit={verifyOtp}>
        <div className="flex justify-center gap-2 sm:gap-3" onPaste={handlePaste}>
          {digits.map((digit, index) => (
            <input
              aria-label={`Chữ số OTP thứ ${index + 1}`}
              autoComplete={index === 0 ? "one-time-code" : "off"}
              autoFocus={index === 0}
              className={cn(
                "h-14 w-11 rounded-xl border bg-white text-center text-xl font-semibold text-slate-950 outline-none transition sm:h-16 sm:w-13 sm:text-2xl",
                error
                  ? "border-rose-300 focus:border-rose-500 focus:ring-4 focus:ring-rose-500/10"
                  : "border-slate-200 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10",
              )}
              inputMode="numeric"
              key={index}
              maxLength={1}
              onChange={(event) => updateDigit(index, event.target.value)}
              onKeyDown={(event) => handleKeyDown(index, event)}
              ref={(element) => { inputRefs.current[index] = element; }}
              value={digit}
            />
          ))}
        </div>

        <Button
          className="mt-7 w-full"
          disabled={isVerifying || digits.some((digit) => !digit)}
          size="lg"
          type="submit"
        >
          {isVerifying ? <Spinner /> : <MailCheck className="size-5" />}
          {isVerifying ? "Đang xác minh..." : "Xác minh Email"}
        </Button>
      </form>

      <div className="mt-5 text-center text-sm text-slate-500">
        {hasOtpBeenSent ? "Không nhận được mã?" : "Bạn chưa có mã?"}{" "}
        <button
          className="inline-flex items-center gap-1 font-medium text-blue-600 transition hover:text-blue-700 disabled:cursor-not-allowed disabled:text-slate-400"
          disabled={countdown > 0 || isResending}
          onClick={() => void resendOtp()}
          type="button"
        >
          {isResending ? <Spinner className="size-3.5" /> : <RotateCcw className="size-3.5" />}
          {isResending
            ? "Đang gửi..."
            : countdown > 0
              ? `Gửi lại sau ${countdown}s`
              : hasOtpBeenSent
                ? "Gửi lại mã"
                : "Gửi mã"}
        </button>
      </div>
    </VerificationShell>
  );
}

function VerificationShell({ children }: { children: React.ReactNode }) {
  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-50 px-4 py-10">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -left-32 top-20 size-80 rounded-full bg-blue-100/70 blur-3xl" />
        <div className="absolute -right-28 bottom-16 size-72 rounded-full bg-amber-100/60 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(to_right,#e2e8f044_1px,transparent_1px),linear-gradient(to_bottom,#e2e8f044_1px,transparent_1px)] bg-[size:32px_32px]" />
      </div>
      <section className="relative w-full max-w-lg rounded-3xl border border-slate-200 bg-white p-7 shadow-2xl shadow-slate-950/[0.08] sm:p-9">
        <Logo className="mb-8 justify-center" />
        {children}
      </section>
    </main>
  );
}
