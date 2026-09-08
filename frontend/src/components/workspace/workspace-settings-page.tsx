"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { useEffect } from "react";

import { Spinner } from "@/components/ui/spinner";
import { WorkspaceMembersSettings } from "@/components/workspace/workspace-members-settings";
import { useWorkspace } from "@/hooks/use-workspace";
import { useShowcase } from "@/hooks/use-showcase";

export function WorkspaceSettingsPage({ workspaceId }: { workspaceId: string }) {
  const { isShowcaseWorkspace, isShowcaseUser } = useShowcase(workspaceId);
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

  if (isShowcaseWorkspace || isShowcaseUser) {
    return (
      <div className="glass-panel rounded-2xl p-6">
        <p className="text-sm text-content">Cài đặt bị khóa trên không gian trải nghiệm công khai.</p>
        <Link className="mt-3 inline-flex min-h-11 items-center rounded-lg text-sm text-accent underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring" href={`/workspaces/${workspace.id}/chat`}>Quay lại trò chuyện</Link>
      </div>
    );
  }
  return <WorkspaceMembersSettings workspace={workspace} />;
}
