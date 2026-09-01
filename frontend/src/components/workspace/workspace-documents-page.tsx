"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { DocumentManager } from "@/components/document/document-manager";
import { Spinner } from "@/components/ui/spinner";
import { useWorkspace } from "@/hooks/use-workspace";

export function WorkspaceDocumentsPage({
  workspaceId,
}: {
  workspaceId: string;
}) {
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
      activeWorkspaceId ? `/workspaces/${activeWorkspaceId}` : "/workspaces",
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
        <div className="flex items-center gap-3 text-sm text-slate-500">
          <Spinner className="size-5 text-blue-600" />
          Đang tải workspace...
        </div>
      </div>
    );
  }

  return <DocumentManager key={workspace.id} workspace={workspace} />;
}
