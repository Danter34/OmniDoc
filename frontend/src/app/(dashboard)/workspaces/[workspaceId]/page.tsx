import type { Metadata } from "next";

import { WorkspaceDocumentsPage } from "@/components/workspace/workspace-documents-page";

export const metadata: Metadata = {
  title: "Tài liệu",
};

export default async function WorkspacePage({
  params,
}: {
  params: Promise<{ workspaceId: string }>;
}) {
  const { workspaceId } = await params;

  return <WorkspaceDocumentsPage workspaceId={workspaceId} />;
}
