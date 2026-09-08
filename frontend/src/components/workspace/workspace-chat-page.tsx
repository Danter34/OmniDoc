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
    error,
    setActiveWorkspaceId,
  } = useWorkspace();
  const workspace = workspaces.find((item) => item.id === workspaceId);

  useEffect(() => {
    if (isLoading || error) {
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
    error,
    router,
    setActiveWorkspaceId,
    workspace,
  ]);

  if (!workspace) {
    if (!isLoading && error) {
      return <p role="alert" className="p-6 text-danger">Không thể mở workspace. {error}</p>;
    }
    return (
      <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center">
        <div className="flex items-center gap-3 text-sm text-muted">
          <Spinner className="size-5 text-accent" />
          Đang mở RAG Chat...
        </div>
      </div>
    );
  }

  return <ChatCanvas key={workspace.id} workspace={workspace} />;
}
