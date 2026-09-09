import type { Metadata } from "next";

import { AuthForm } from "@/components/auth/auth-form";
import { safeReturnUrl } from "@/lib/return-url";

export const metadata: Metadata = {
  title: "Đăng ký",
};

export default async function RegisterPage({
  searchParams,
}: {
  searchParams: Promise<{ returnUrl?: string; redirect?: string }>;
}) {
  const { returnUrl, redirect } = await searchParams;
  const redirectTo = safeReturnUrl(returnUrl ?? redirect);

  return <AuthForm mode="register" redirectTo={redirectTo} />;
}
