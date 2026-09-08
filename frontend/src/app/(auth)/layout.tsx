import type { ReactNode } from "react";

import { AuthShell } from "@/components/auth/auth-shell";

/**
 * Dark-themed layout for all onboarding / auth routes.
 * Keeps the rest of the app (workspace pages) free to use the
 * user's chosen light / dark / system preference.
 */
export default function AuthLayout({ children }: { children: ReactNode }) {
  return <AuthShell>{children}</AuthShell>;
}
