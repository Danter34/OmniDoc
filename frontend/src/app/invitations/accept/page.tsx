import type { Metadata } from "next";

import { InvitationAcceptancePage } from "@/components/invitation/invitation-acceptance-page";

export const metadata: Metadata = {
  title: "Lời mời Workspace",
};

export default async function AcceptInvitationPage({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;

  return <InvitationAcceptancePage token={token ?? ""} />;
}
