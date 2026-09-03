import { apiRequest } from "@/services/api-client";
import type {
  CreateWorkspaceRequest,
  InviteWorkspaceMemberRequest,
  Workspace,
  WorkspaceInvitation,
  WorkspaceMember,
  WorkspaceRole,
} from "@/types/workspace.types";

export const workspaceService = {
  getAll(signal?: AbortSignal) {
    return apiRequest<Workspace[]>("/api/workspaces", { signal });
  },

  create(payload: CreateWorkspaceRequest) {
    return apiRequest<Workspace>("/api/workspaces", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  getMembers(workspaceId: string, signal?: AbortSignal) {
    return apiRequest<WorkspaceMember[]>(
      `/api/workspaces/${workspaceId}/members`,
      { signal },
    );
  },

  inviteMember(
    workspaceId: string,
    payload: InviteWorkspaceMemberRequest,
  ) {
    return apiRequest<WorkspaceInvitation>(
      `/api/workspaces/${workspaceId}/invitations`,
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    );
  },

  updateMemberRole(
    workspaceId: string,
    memberUserId: string,
    role: WorkspaceRole,
  ) {
    return apiRequest<WorkspaceMember>(
      `/api/workspaces/${workspaceId}/members/${memberUserId}/role`,
      {
        method: "PATCH",
        body: JSON.stringify({ role }),
      },
    );
  },

  removeMember(workspaceId: string, memberUserId: string) {
    return apiRequest<boolean>(
      `/api/workspaces/${workspaceId}/members/${memberUserId}`,
      { method: "DELETE" },
    );
  },
};
