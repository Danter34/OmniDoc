"use client";

import { MailWarning, X } from "lucide-react";
import { useState } from "react";

import { useAuth } from "@/hooks/use-auth";

export function EmailVerificationBanner() {
  const { user, isLoading, openVerificationModal } = useAuth();
  const [dismissed, setDismissed] = useState(false);

  if (isLoading || !user || user.emailConfirmed || dismissed) {
    return null;
  }

  return (
    <div className="border-b border-warning bg-warning-subtle px-4 py-2.5 text-warning">
      <div className="mx-auto flex w-full max-w-[1440px] items-center justify-center gap-2 text-center text-sm sm:px-2">
        <MailWarning className="size-4 shrink-0" />
        <span>Email của bạn chưa được xác minh.</span>
        <button
          className="min-h-11 font-semibold underline decoration-current underline-offset-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
          onClick={openVerificationModal}
          type="button"
        >
          Xác minh ngay
        </button>
        <button
          aria-label="Ẩn thông báo xác minh email"
          className="ml-auto inline-flex size-11 shrink-0 items-center justify-center rounded-lg transition-colors hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
          onClick={() => setDismissed(true)}
          type="button"
        >
          <X className="size-4" />
        </button>
      </div>
    </div>
  );
}
