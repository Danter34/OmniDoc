import type { Metadata } from "next";

import { EmailVerificationPage } from "@/components/auth/email-verification-page";
import { ProtectedRoute } from "@/components/auth/protected-route";

export const metadata: Metadata = {
  title: "Xác minh Email",
};

export default async function VerifyEmailPage({
  searchParams,
}: {
  searchParams: Promise<{ redirect?: string }>;
}) {
  const { redirect } = await searchParams;
  const redirectTo =
    redirect?.startsWith("/") && !redirect.startsWith("//")
      ? redirect
      : "/workspaces";

  return (
    <ProtectedRoute>
      <EmailVerificationPage redirectTo={redirectTo} />
    </ProtectedRoute>
  );
}
