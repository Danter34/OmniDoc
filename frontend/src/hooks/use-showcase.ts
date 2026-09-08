"use client";

import { useAuth } from "@/hooks/use-auth";
import { useWorkspace } from "@/hooks/use-workspace";
import { getShowcaseWorkspaceId, isShowcaseUser, showcase } from "@/lib/showcase";

// Presentation hints only; server-side IShowcasePolicy remains authoritative.
export function useShowcase(workspaceId?: string | null) {
  const { user } = useAuth();
  const { workspaces } = useWorkspace();
  const isSampleUser = isShowcaseUser(user);
  const sampleWorkspaceId = isSampleUser
    ? getShowcaseWorkspaceId(workspaces)
    : showcase.workspaceId ?? "b4987f7e-48cc-4ba5-a117-10ac4cbced02";
  return {
    isShowcaseUser: isSampleUser,
    isShowcaseWorkspace: showcase.enabled && Boolean(workspaceId) && workspaceId === sampleWorkspaceId,
  };
}
