"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { getErrorMessage } from "@/services/api-client";
import { documentService } from "@/services/document.service";
import type {
  DocumentDto,
  DocumentStage,
  WorkspaceDocument,
} from "@/types/document.types";
import type { DocumentProgressUpdate } from "@/types/signalr.types";

function getInitialProgress(document: DocumentDto) {
  if (document.status === "Indexed") {
    return 100;
  }

  if (document.status === "Failed") {
    return -1;
  }

  if (document.status === "Processing") {
    return 10;
  }

  return 0;
}

function toWorkspaceDocument(document: DocumentDto): WorkspaceDocument {
  return {
    ...document,
    stage: document.status as DocumentStage,
    progress: getInitialProgress(document),
  };
}

function applyUpdate(
  document: WorkspaceDocument,
  update: DocumentProgressUpdate,
): WorkspaceDocument {
  const isComplete =
    update.stage === "Completed" ||
    update.stage === "Indexed" ||
    update.percent >= 100;
  const isFailed = update.stage === "Failed" || update.percent < 0;

  return {
    ...document,
    status: isFailed ? "Failed" : isComplete ? "Indexed" : "Processing",
    stage: isComplete ? "Indexed" : update.stage,
    progress: isFailed ? -1 : Math.min(100, Math.max(0, update.percent)),
    errorMessage: update.errorMessage,
  };
}

export function useDocuments(workspaceId: string) {
  const [documents, setDocuments] = useState<WorkspaceDocument[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const latestProgressRef = useRef(new Map<string, DocumentProgressUpdate>());

  const mergeLatestProgress = useCallback((items: DocumentDto[]) => {
    return items.map((item) => {
      const document = toWorkspaceDocument(item);
      const latestUpdate = latestProgressRef.current.get(item.id);
      return latestUpdate ? applyUpdate(document, latestUpdate) : document;
    });
  }, []);

  const loadDocuments = useCallback(
    async (signal?: AbortSignal) => {
      setIsLoading(true);
      setError(null);

      try {
        const items = await documentService.getAll(workspaceId, signal);
        setDocuments(mergeLatestProgress(items));
      } catch (requestError) {
        if (
          requestError instanceof DOMException &&
          requestError.name === "AbortError"
        ) {
          return;
        }

        setError(getErrorMessage(requestError));
      } finally {
        if (!signal?.aborted) {
          setIsLoading(false);
        }
      }
    },
    [mergeLatestProgress, workspaceId],
  );

  useEffect(() => {
    const controller = new AbortController();
    latestProgressRef.current.clear();

    documentService
      .getAll(workspaceId, controller.signal)
      .then((items) => {
        setDocuments(mergeLatestProgress(items));
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (
          requestError instanceof DOMException &&
          requestError.name === "AbortError"
        ) {
          return;
        }

        setError(getErrorMessage(requestError));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [mergeLatestProgress, workspaceId]);

  const applyProgressUpdates = useCallback(
    (updates: DocumentProgressUpdate[]) => {
      if (updates.length === 0) {
        return;
      }

      updates.forEach((update) => {
        latestProgressRef.current.set(update.documentId, update);
      });

      const updatesById = new Map(
        updates.map((update) => [update.documentId, update]),
      );

      setDocuments((current) => {
        let changed = false;
        const next = current.map((document) => {
          const update = updatesById.get(document.id);

          if (!update) {
            return document;
          }

          changed = true;
          return applyUpdate(document, update);
        });

        return changed ? next : current;
      });
    },
    [],
  );

  const uploadDocument = useCallback(
    async (file: File) => {
      const uploaded = await documentService.upload(workspaceId, file);
      let document = toWorkspaceDocument(uploaded);
      const latestUpdate = latestProgressRef.current.get(uploaded.id);

      if (latestUpdate) {
        document = applyUpdate(document, latestUpdate);
      }

      setDocuments((current) => {
        if (current.some((item) => item.id === document.id)) {
          return current.map((item) =>
            item.id === document.id ? document : item,
          );
        }

        return [document, ...current];
      });

      return document;
    },
    [workspaceId],
  );

  return {
    documents,
    isLoading,
    error,
    uploadDocument,
    applyProgressUpdates,
    reload: loadDocuments,
  };
}
