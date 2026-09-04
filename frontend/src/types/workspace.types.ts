export type WorkspaceRole = "Owner" | "Admin" | "Member";

export interface Workspace {
  id: string;
  name: string;
  description: string | null;
  createdAtUtc: string;
  documentCount: number;
  role: WorkspaceRole;
}

export interface CreateWorkspaceRequest {
  name: string;
  description?: string;
}

export interface WorkspaceMember {
  userId: string;
  fullName: string;
  email: string;
  role: WorkspaceRole;
  joinedAt: string;
}

export interface InviteWorkspaceMemberRequest {
  email: string;
  role: WorkspaceRole;
}

export interface WorkspaceInvitation {
  id: string;
  workspaceId: string;
  inviteeEmail: string;
  role: WorkspaceRole;
  expiresAt: string;
  status: InvitationStatus;
  inviteLink: string;
}

export type InvitationStatus =
  | "Pending"
  | "Accepted"
  | "Revoked"
  | "Expired";

export interface InvitationDetails {
  workspaceId: string;
  workspaceName: string;
  inviterName: string;
  role: WorkspaceRole;
  expiresAt: string;
  status: InvitationStatus;
}

export interface AcceptedInvitation {
  workspaceId: string;
  workspaceName: string;
  role: WorkspaceRole;
}
