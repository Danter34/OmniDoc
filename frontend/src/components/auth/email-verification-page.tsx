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
          <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-success-subtle text-success">
            <CheckCircle2 className="size-8" />
          </div>
          <h1 className="mt-5 text-2xl font-semibold text-content">Email đã được xác minh</h1>
          <p className="mt-2 text-sm leading-6 text-muted">Tài khoản của bạn đã sẵn sàng. Đang chuyển về workspace...</p>
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
        <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-info-subtle text-accent">
          <MailCheck className="size-7" />
        </div>
        <p className="mt-5 text-sm font-medium text-accent">Xác minh tài khoản</p>
        <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-content">Nhập mã xác minh</h1>
        <p className="mt-3 text-sm leading-6 text-muted">
          {hasOtpBeenSent ? "Mã OTP 6 số đã được gửi tới " : "Gửi mã OTP 6 số tới "}
          <strong className="font-medium text-content-secondary">{user?.email}</strong>.
          {hasOtpBeenSent ? " Mã có hiệu lực trong 10 phút." : " để bắt đầu xác minh."}
        </p>
      </div>

      {error ? (
        <div className="mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger" role="alert">
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
                "h-14 w-11 rounded-xl border bg-surface text-center text-xl font-semibold text-content outline-none transition sm:h-16 sm:w-13 sm:text-2xl",
                error
                  ? "border-danger focus:border-danger focus:ring-4 focus:ring-danger-glow"
                  : "border-line-subtle focus:border-focus-ring focus:ring-4 focus:ring-focus-glow",
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

      <div className="mt-5 text-center text-sm text-muted">
        {hasOtpBeenSent ? "Không nhận được mã?" : "Bạn chưa có mã?"}{" "}
        <button
          className="inline-flex min-h-11 items-center gap-1 font-medium text-accent transition-colors hover:text-accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring disabled:cursor-not-allowed disabled:text-muted"
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
    <main className="ambient-bg relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-10">
      <section className="glass-panel relative w-full max-w-lg rounded-3xl p-7 sm:p-9">
        <Logo className="mb-8 justify-center" />
        {children}
      </section>
    </main>
  );
}
