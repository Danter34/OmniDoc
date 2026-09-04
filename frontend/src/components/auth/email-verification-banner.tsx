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
    <div className="border-b border-amber-200 bg-amber-50 px-4 py-2.5 text-amber-900">
      <div className="mx-auto flex w-full max-w-[1440px] items-center justify-center gap-2 text-center text-sm sm:px-2">
        <MailWarning className="size-4 shrink-0 text-amber-600" />
        <span>Email của bạn chưa được xác minh.</span>
        <button
          className="font-semibold text-amber-900 underline decoration-amber-400 underline-offset-2 hover:text-amber-700"
          onClick={openVerificationModal}
          type="button"
        >
          Xác minh ngay
        </button>
        <button
          aria-label="Ẩn thông báo xác minh email"
          className="ml-auto inline-flex size-7 shrink-0 items-center justify-center rounded-lg text-amber-700 transition hover:bg-amber-100 hover:text-amber-950"
          onClick={() => setDismissed(true)}
          type="button"
        >
          <X className="size-4" />
        </button>
      </div>
    </div>
  );
}
