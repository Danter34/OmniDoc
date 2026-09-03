import type { Metadata } from "next";

import { InvitationAcceptancePage } from "@/components/invitation/invitation-acceptance-page";

export const metadata: Metadata = {
  title: "Lời mời Workspace",
};

export default async function InvitationPage({
  params,
}: {
  params: Promise<{ token: string }>;
}) {
  const { token } = await params;

  return <InvitationAcceptancePage token={token} />;
}
