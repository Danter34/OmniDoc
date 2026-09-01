export type ChatRole = "User" | "Assistant";
export type ChatMessageStatus =
  | "complete"
  | "streaming"
  | "stopped"
  | "error";

export interface Conversation {
  id: string;
  workspaceId: string;
  title: string;
  createdAtUtc: string;
  lastActivityAtUtc: string;
}

export interface Citation {
  chunkId: string;
  documentId: string;
  documentName: string;
  pageNumber: number;
  snippet: string;
  similarityScore: number;
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  role: ChatRole;
  content: string;
  createdAtUtc: string;
  citations: Citation[];
  status?: ChatMessageStatus;
}

export interface CreateConversationRequest {
  title: string;
}

export interface SendChatMessageRequest {
  conversationId: string | null;
  message: string;
  topK?: number;
}

export interface ChatStreamEvent {
  type: "token" | "citation" | "done" | "error";
  content?: string;
  citation?: Citation;
  conversationId?: string;
  messageId?: string;
}
