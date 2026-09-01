import { apiRequest } from "@/services/api-client";
import type { DocumentDto } from "@/types/document.types";

export const documentService = {
  getAll(workspaceId: string, signal?: AbortSignal) {
    return apiRequest<DocumentDto[]>(
      `/api/workspaces/${workspaceId}/documents`,
      { signal },
    );
  },

  upload(workspaceId: string, file: File) {
    const body = new FormData();
    body.append("file", file);

    return apiRequest<DocumentDto>(
      `/api/workspaces/${workspaceId}/documents`,
      {
        method: "POST",
        body,
      },
    );
  },
};
