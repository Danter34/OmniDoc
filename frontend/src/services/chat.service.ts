import {
  ApiError,
  UNAUTHORIZED_EVENT,
  apiFetch,
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
      const text = await response.text();

      if (response.status === 401 && typeof window !== "undefined") {
        window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
      }

      throw new ApiError(
        text || `Không thể bắt đầu luồng trả lời (${response.status}).`,
        response.status,
      );
    }

    if (!response.body) {
      throw new ApiError("Trình duyệt không nhận được SSE response body.", 500);
    }

    return response;
  },
};
