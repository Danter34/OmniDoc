import type { ReactNode } from "react";

import { ProtectedRoute } from "@/components/auth/protected-route";
import { DashboardShell } from "@/components/workspace/dashboard-shell";
import { WorkspaceProvider } from "@/components/workspace/workspace-provider";

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <ProtectedRoute>
      <WorkspaceProvider>
        <DashboardShell>{children}</DashboardShell>
      </WorkspaceProvider>
    </ProtectedRoute>
  );
}
