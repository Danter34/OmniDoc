import type { Metadata } from "next";

import { AuthForm } from "@/components/auth/auth-form";
import { safeReturnUrl } from "@/lib/return-url";

export const metadata: Metadata = {
  title: "Đăng nhập",
};

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ returnUrl?: string; redirect?: string; mode?: string }>;
}) {
  const { returnUrl, redirect, mode } = await searchParams;
  const redirectTo = safeReturnUrl(returnUrl ?? redirect);

  return <AuthForm mode="login" redirectTo={redirectTo} highlightShowcase={mode === "showcase"} />;
}
