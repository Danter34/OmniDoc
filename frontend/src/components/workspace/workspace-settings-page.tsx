"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { Spinner } from "@/components/ui/spinner";
import { WorkspaceMembersSettings } from "@/components/workspace/workspace-members-settings";
import { useWorkspace } from "@/hooks/use-workspace";

export function WorkspaceSettingsPage({ workspaceId }: { workspaceId: string }) {
  const router = useRouter();
  const {
    workspaces,
    activeWorkspaceId,
    isLoading,
    setActiveWorkspaceId,
  } = useWorkspace();
  const workspace = workspaces.find((item) => item.id === workspaceId);

  useEffect(() => {
    if (isLoading) {
      return;
    }

    if (workspace) {
      if (activeWorkspaceId !== workspace.id) {
        setActiveWorkspaceId(workspace.id);
      }
      return;
    }

    router.replace(
      activeWorkspaceId
        ? `/workspaces/${activeWorkspaceId}/settings`
        : "/workspaces",
    );
  }, [
    activeWorkspaceId,
    isLoading,
    router,
    setActiveWorkspaceId,
    workspace,
  ]);

  if (isLoading || !workspace) {
    return (
      <div className="flex min-h-[calc(100vh-9rem)] items-center justify-center">
        <div className="flex items-center gap-3 text-sm text-muted">
          <Spinner className="size-5 text-accent" />
          Đang tải workspace...
        </div>
      </div>
    );
  }

  return <WorkspaceMembersSettings workspace={workspace} />;
}
