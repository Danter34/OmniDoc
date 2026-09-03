import type { Metadata } from "next";

import { AuthForm } from "@/components/auth/auth-form";

export const metadata: Metadata = {
  title: "Đăng ký",
};

export default async function RegisterPage({
  searchParams,
}: {
  searchParams: Promise<{ redirect?: string }>;
}) {
  const { redirect } = await searchParams;
  const redirectTo =
    redirect?.startsWith("/") && !redirect.startsWith("//")
      ? redirect
      : "/workspaces";

  return <AuthForm mode="register" redirectTo={redirectTo} />;
}
