"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { ChatCanvas } from "@/components/chat/chat-canvas";
import { Spinner } from "@/components/ui/spinner";
import { useWorkspace } from "@/hooks/use-workspace";

export function WorkspaceChatPage({ workspaceId }: { workspaceId: string }) {
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
      activeWorkspaceId ? `/workspaces/${activeWorkspaceId}/chat` : "/workspaces",
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
      <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center">
        <div className="flex items-center gap-3 text-sm text-slate-500">
          <Spinner className="size-5 text-blue-600" />
          Đang mở RAG Chat...
        </div>
      </div>
    );
  }

  return <ChatCanvas key={workspace.id} workspace={workspace} />;
}
