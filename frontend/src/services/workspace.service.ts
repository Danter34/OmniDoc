import { apiRequest } from "@/services/api-client";
import type {
  CreateWorkspaceRequest,
  Workspace,
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
};
