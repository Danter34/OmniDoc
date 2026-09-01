import type { Metadata } from "next";

import { WorkspaceChatPage } from "@/components/workspace/workspace-chat-page";

export const metadata: Metadata = {
  title: "RAG Chat",
};

export default async function ChatPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>;
}) {
  const { workspaceId } = await params;

  return <WorkspaceChatPage workspaceId={workspaceId} />;
}
