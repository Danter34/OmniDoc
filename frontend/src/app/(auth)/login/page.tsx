import type { Metadata } from "next";

import { AuthForm } from "@/components/auth/auth-form";

export const metadata: Metadata = {
  title: "Đăng nhập",
};

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ redirect?: string }>;
}) {
  const { redirect } = await searchParams;
  const redirectTo =
    redirect?.startsWith("/") && !redirect.startsWith("//")
      ? redirect
      : "/workspaces";

  return <AuthForm mode="login" redirectTo={redirectTo} />;
}
