import type { ReactNode } from "react";

import { AuthShell } from "@/components/auth/auth-shell";

export default function InvitationsLayout({ children }: { children: ReactNode }) {
  return <AuthShell>{children}</AuthShell>;
}
