import { apiRequest } from "@/services/api-client";
import type {
  ChatMessage,
  Conversation,
  CreateConversationRequest,
} from "@/types/chat.types";

export const conversationService = {
  getAll(workspaceId: string, signal?: AbortSignal) {
    return apiRequest<Conversation[]>(
      `/api/workspaces/${workspaceId}/conversations`,
      { signal },
    );
  },

  create(workspaceId: string, payload: CreateConversationRequest) {
    return apiRequest<Conversation>(
      `/api/workspaces/${workspaceId}/conversations`,
      {
        method: "POST",
        body: JSON.stringify(payload),
      },
    );
  },

  delete(workspaceId: string, conversationId: string) {
    return apiRequest<boolean>(
      `/api/workspaces/${workspaceId}/conversations/${conversationId}`,
      { method: "DELETE" },
    );
  },

  getMessages(
    workspaceId: string,
    conversationId: string,
    signal?: AbortSignal,
  ) {
    return apiRequest<ChatMessage[]>(
      `/api/workspaces/${workspaceId}/conversations/${conversationId}/messages`,
      { signal },
    );
  },
};
