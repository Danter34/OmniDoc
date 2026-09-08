import {
  ApiError,
  apiFetch,
  readApiError,
} from "@/services/api-client";
import type { SendChatMessageRequest } from "@/types/chat.types";

export const chatService = {
  async stream(
    workspaceId: string,
    payload: SendChatMessageRequest,
    signal: AbortSignal,
  ) {
    const response = await apiFetch(
      `/api/workspaces/${workspaceId}/chat/stream`,
      {
        method: "POST",
        body: JSON.stringify(payload),
        signal,
      },
    );

    if (!response.ok) {
      throw await readApiError(response);
    }

    if (!response.body) {
      throw new ApiError("Trình duyệt không nhận được SSE response body.", 500);
    }

    return response;
  },
};
