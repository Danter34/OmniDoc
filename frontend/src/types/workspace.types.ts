export type WorkspaceRole = "Owner" | "Member";

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
