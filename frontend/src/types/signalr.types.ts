import type { DocumentStage } from "@/types/document.types";

export type RealtimeConnectionStatus =
  | "idle"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "disconnected";

export interface DocumentProgressUpdate {
  documentId: string;
  workspaceId?: string;
  stage: DocumentStage;
  percent: number;
  errorMessage: string | null;
}

export interface DocumentProgressPayload {
  documentId: string;
  workspaceId?: string;
  stage: DocumentStage;
  progressPercentage?: number;
  percent?: number;
  errorMessage?: string | null;
}
