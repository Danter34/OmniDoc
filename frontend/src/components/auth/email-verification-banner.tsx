"use client";

import { MailWarning } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { useAuth } from "@/hooks/use-auth";

export function EmailVerificationBanner() {
  const { user, isLoading } = useAuth();
  const pathname = usePathname();

  if (isLoading || !user || user.emailConfirmed) {
    return null;
  }

  const redirect = pathname || "/workspaces";

  return (
    <div className="border-b border-amber-200 bg-amber-50 px-4 py-2.5 text-amber-900">
      <div className="mx-auto flex max-w-[1440px] items-center justify-center gap-2 text-center text-sm sm:px-2">
        <MailWarning className="size-4 shrink-0 text-amber-600" />
        <span>Email của bạn chưa được xác minh.</span>
        <Link
          className="font-semibold text-amber-900 underline decoration-amber-400 underline-offset-2 hover:text-amber-700"
          href={`/verify-email?redirect=${encodeURIComponent(redirect)}`}
        >
          Bấm vào đây để xác minh
        </Link>
      </div>
    </div>
  );
}
