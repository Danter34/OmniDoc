"use client";

import { useSignalR } from "@/hooks/use-signalr";
import type { DocumentProgressUpdate } from "@/types/signalr.types";

export function useDocumentProgress(
  workspaceId: string,
  onProgress: (updates: DocumentProgressUpdate[]) => void,
) {
  return useSignalR(workspaceId, onProgress);
}
