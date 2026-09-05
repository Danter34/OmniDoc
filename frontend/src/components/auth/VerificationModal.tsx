"use client";

import {
  AlertCircle,
  CheckCircle2,
  MailCheck,
  RotateCcw,
  Zap,
} from "lucide-react";
import {
  useEffect,
  useRef,
  useState,
  type ClipboardEvent,
  type FormEvent,
  type KeyboardEvent,
} from "react";

import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { cn } from "@/lib/utils";
import { ApiError, getErrorMessage } from "@/services/api-client";
import { authService } from "@/services/auth.service";

const OTP_LENGTH = 6;

function secondsUntil(value?: string | null) {
  if (!value) {
    return 0;
  }

  return Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000));
}

export function VerificationModal({ onClose }: { onClose: () => void }) {
  const { user, refreshUser } = useAuth();
  const [initialResendAvailableAt] = useState(user?.otpResendAvailableAt);
  const inputRefs = useRef<Array<HTMLInputElement | null>>([]);
  const closeTimerRef = useRef<number | null>(null);
  const [digits, setDigits] = useState<string[]>(() =>
    Array(OTP_LENGTH).fill(""),
  );
  const [countdown, setCountdown] = useState(() =>
    secondsUntil(user?.otpResendAvailableAt),
  );
  const [debugOtp, setDebugOtp] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoadingOtp, setIsLoadingOtp] = useState(true);
  const [isResending, setIsResending] = useState(false);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isVerified, setIsVerified] = useState(false);

  useEffect(() => {
    let active = true;

    async function loadOtp() {
      try {
        const response = await authService.sendVerificationOtp();
        if (!active) return;

        setDebugOtp(response.debugOtp);
        setCountdown(
          response.resendCooldownSeconds ||
            secondsUntil(response.resendAvailableAt),
        );
      } catch (requestError) {
        if (!active) return;

        if (requestError instanceof ApiError && requestError.status === 429) {
          setCountdown(secondsUntil(initialResendAvailableAt));
        } else {
          setError(getErrorMessage(requestError));
        }
      } finally {
        if (active) setIsLoadingOtp(false);
      }
    }

    void loadOtp();

    return () => {
      active = false;
      if (closeTimerRef.current !== null) {
        window.clearTimeout(closeTimerRef.current);
      }
    };
  }, [initialResendAvailableAt]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      setCountdown((current) => Math.max(0, current - 1));
    }, 1000);

    return () => window.clearInterval(timer);
  }, []);

  function focusInput(index: number) {
    inputRefs.current[index]?.focus();
    inputRefs.current[index]?.select();
  }

  function applyOtp(value: string) {
    const numeric = value.replace(/\D/g, "").slice(0, OTP_LENGTH);
    const next = Array(OTP_LENGTH).fill("") as string[];

    numeric.split("").forEach((digit, index) => {
      next[index] = digit;
    });

    setDigits(next);
    focusInput(Math.min(numeric.length, OTP_LENGTH - 1));
  }

  function updateDigit(index: number, value: string) {
    const numeric = value.replace(/\D/g, "");

    if (numeric.length > 1) {
      applyOtp(numeric);
      return;
    }

    setDigits((current) => {
      const next = [...current];
      next[index] = numeric;
      return next;
    });

    if (numeric && index < OTP_LENGTH - 1) {
      focusInput(index + 1);
    }
  }

  function handlePaste(event: ClipboardEvent<HTMLDivElement>) {
    event.preventDefault();
    applyOtp(event.clipboardData.getData("text"));
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
      focusInput(targetIndex);
    } else if (event.key === "ArrowLeft" && index > 0) {
      event.preventDefault();
      focusInput(index - 1);
    } else if (event.key === "ArrowRight" && index < OTP_LENGTH - 1) {
      event.preventDefault();
      focusInput(index + 1);
    }
  }

  async function verifyWithOtp(otp: string) {
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
      closeTimerRef.current = window.setTimeout(onClose, 1100);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      setDigits(Array(OTP_LENGTH).fill(""));
      focusInput(0);
    } finally {
      setIsVerifying(false);
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void verifyWithOtp(digits.join(""));
  }

  async function resendOtp() {
    setIsResending(true);
    setError(null);

    try {
      const response = await authService.sendVerificationOtp();
      setDebugOtp(response.debugOtp);
      setCountdown(
        response.resendCooldownSeconds || secondsUntil(response.resendAvailableAt),
      );
      setDigits(Array(OTP_LENGTH).fill(""));
      focusInput(0);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsResending(false);
    }
  }

  async function handleDemoOtp() {
    if (!debugOtp) return;

    applyOtp(debugOtp);
    await verifyWithOtp(debugOtp);
  }

  return (
    <Modal
      description={
        isVerified
          ? "Tài khoản đã sẵn sàng sử dụng các tính năng bảo mật."
          : `Nhập mã OTP 6 số đã gửi tới ${user?.email ?? "email của bạn"}. Mã có hiệu lực trong 10 phút.`
      }
      onClose={onClose}
      open
      title={isVerified ? "Xác minh thành công" : "Xác minh email"}
    >
      {isVerified ? (
        <div className="py-4 text-center" role="status">
          <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-success-subtle text-success">
            <CheckCircle2 className="size-8" />
          </div>
          <p className="mt-4 text-sm font-medium text-success">
            Email của bạn đã được xác minh.
          </p>
        </div>
      ) : (
        <>
          {error ? (
            <div
              className="mb-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger"
              role="alert"
            >
              <AlertCircle className="mt-0.5 size-4 shrink-0" />
              <span>{error}</span>
            </div>
          ) : null}

          <form onSubmit={handleSubmit}>
            <div
              className="flex justify-center gap-2 sm:gap-3"
              onPaste={handlePaste}
            >
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
                  disabled={isVerifying || isLoadingOtp}
                  inputMode="numeric"
                  key={index}
                  maxLength={1}
                  onChange={(event) => updateDigit(index, event.target.value)}
                  onKeyDown={(event) => handleKeyDown(index, event)}
                  ref={(element) => {
                    inputRefs.current[index] = element;
                  }}
                  value={digit}
                />
              ))}
            </div>

            <Button
              className="mt-6 w-full"
              disabled={
                isLoadingOtp ||
                isVerifying ||
                digits.some((digit) => !digit)
              }
              size="lg"
              type="submit"
            >
              {isVerifying || isLoadingOtp ? (
                <Spinner />
              ) : (
                <MailCheck className="size-5" />
              )}
              {isVerifying
                ? "Đang xác minh..."
                : isLoadingOtp
                  ? "Đang chuẩn bị mã..."
                  : "Xác nhận"}
            </Button>
          </form>

          <div className="mt-4 text-center text-sm text-muted">
            Không nhận được mã?{" "}
            <button
              className="inline-flex min-h-11 items-center gap-1 font-medium text-accent transition-colors hover:text-accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring disabled:cursor-not-allowed disabled:text-muted"
              disabled={countdown > 0 || isResending || isLoadingOtp}
              onClick={() => void resendOtp()}
              type="button"
            >
              {isResending ? (
                <Spinner className="size-3.5" />
              ) : (
                <RotateCcw className="size-3.5" />
              )}
              {isResending
                ? "Đang gửi..."
                : countdown > 0
                  ? `Gửi lại sau ${countdown}s`
                  : "Gửi lại mã"}
            </button>
          </div>

          {debugOtp ? (
            <div className="mt-5 border-t border-dashed border-line-subtle pt-5 text-center">
              <button
                className="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-warning bg-warning-subtle px-3 py-2 text-sm font-medium text-warning transition-[filter,box-shadow] hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring disabled:opacity-50"
                disabled={isVerifying}
                onClick={() => void handleDemoOtp()}
                type="button"
              >
                <Zap className="size-4" />
                Demo: Tự động điền mã
              </button>
              <p className="mt-1 text-xs text-muted">
                Chỉ hiển thị trong môi trường Development.
              </p>
            </div>
          ) : null}
        </>
      )}
    </Modal>
  );
}
