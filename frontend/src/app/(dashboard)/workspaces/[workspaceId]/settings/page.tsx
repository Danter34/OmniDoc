import type { Metadata } from "next";

import { WorkspaceSettingsPage } from "@/components/workspace/workspace-settings-page";

export const metadata: Metadata = {
  title: "Cài đặt Workspace",
};

export default async function SettingsPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>;
}) {
  const { workspaceId } = await params;

  return <WorkspaceSettingsPage workspaceId={workspaceId} />;
}
