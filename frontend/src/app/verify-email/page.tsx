import type { Metadata } from "next";

import { EmailVerificationPage } from "@/components/auth/email-verification-page";
import { ProtectedRoute } from "@/components/auth/protected-route";
import { safeReturnUrl } from "@/lib/return-url";

export const metadata: Metadata = {
  title: "Xác minh Email",
};

export default async function VerifyEmailPage({
  searchParams,
}: {
  searchParams: Promise<{ returnUrl?: string; redirect?: string }>;
}) {
  const { returnUrl, redirect } = await searchParams;
  const redirectTo = safeReturnUrl(returnUrl ?? redirect);

  return (
    <ProtectedRoute>
      <EmailVerificationPage redirectTo={redirectTo} />
    </ProtectedRoute>
  );
}
