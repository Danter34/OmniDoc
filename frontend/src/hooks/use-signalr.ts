"use client";

import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";

import { useAuth } from "@/hooks/use-auth";
import { getRealtimeUrl } from "@/services/api-client";
import type {
  DocumentProgressPayload,
  DocumentProgressUpdate,
  RealtimeConnectionStatus,
} from "@/types/signalr.types";

const PROGRESS_EVENT = "DocumentProgressUpdated";

function normalizeProgressArguments(
  args: unknown[],
): DocumentProgressUpdate | null {
  const first = args[0];

  if (first && typeof first === "object") {
    const payload = first as Partial<DocumentProgressPayload>;

    if (!payload.documentId || !payload.stage) {
      return null;
    }

    return {
      documentId: payload.documentId,
      workspaceId: payload.workspaceId,
      stage: payload.stage,
      percent: Number(payload.progressPercentage ?? payload.percent ?? 0),
      errorMessage: payload.errorMessage ?? null,
    };
  }

  const [documentId, stage, percent, errorMessage] = args;

  if (
    typeof documentId !== "string" ||
    typeof stage !== "string" ||
    typeof percent !== "number"
  ) {
    return null;
  }

  return {
    documentId,
    stage: stage as DocumentProgressUpdate["stage"],
    percent,
    errorMessage: typeof errorMessage === "string" ? errorMessage : null,
  };
}

export function useSignalR(
  workspaceId: string,
  onProgress: (updates: DocumentProgressUpdate[]) => void,
) {
  const { token } = useAuth();
  const [status, setStatus] =
    useState<RealtimeConnectionStatus>("idle");
  const onProgressRef = useRef(onProgress);

  useEffect(() => {
    onProgressRef.current = onProgress;
  }, [onProgress]);

  useEffect(() => {
    if (!workspaceId || !token) {
      return;
    }

    let disposed = false;
    let animationFrame: number | null = null;
    const queuedUpdates = new Map<string, DocumentProgressUpdate>();
    const connection = new HubConnectionBuilder()
      .withUrl(getRealtimeUrl("/hubs/document-progress"), {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
      .configureLogging(
        process.env.NODE_ENV === "development"
          ? LogLevel.Warning
          : LogLevel.Error,
      )
      .build();

    const flushUpdates = () => {
      animationFrame = null;

      if (disposed || queuedUpdates.size === 0) {
        return;
      }

      const updates = Array.from(queuedUpdates.values());
      queuedUpdates.clear();
      onProgressRef.current(updates);
    };

    const handleProgress = (...args: unknown[]) => {
      const update = normalizeProgressArguments(args);

      if (!update || (update.workspaceId && update.workspaceId !== workspaceId)) {
        return;
      }

      queuedUpdates.set(update.documentId, update);

      if (animationFrame === null) {
        animationFrame = window.requestAnimationFrame(flushUpdates);
      }
    };

    connection.on(PROGRESS_EVENT, handleProgress);
    connection.onreconnecting(() => {
      if (!disposed) {
        setStatus("reconnecting");
      }
    });
    connection.onreconnected(async () => {
      if (disposed) {
        return;
      }

      setStatus("connected");

      try {
        await connection.invoke("JoinWorkspace", workspaceId);
      } catch {
        if (!disposed) {
          setStatus("disconnected");
        }
      }
    });
    connection.onclose(() => {
      if (!disposed) {
        setStatus("disconnected");
      }
    });

    async function startConnection() {
      setStatus("connecting");

      try {
        await connection.start();

        if (disposed) {
          await connection.stop();
          return;
        }

        await connection.invoke("JoinWorkspace", workspaceId);
        setStatus("connected");
      } catch {
        if (!disposed) {
          setStatus("disconnected");
        }
      }
    }

    void startConnection();

    return () => {
      disposed = true;

      if (animationFrame !== null) {
        window.cancelAnimationFrame(animationFrame);
      }

      queuedUpdates.clear();
      connection.off(PROGRESS_EVENT, handleProgress);

      void (async () => {
        if (connection.state === HubConnectionState.Connected) {
          try {
            await connection.invoke("LeaveWorkspace", workspaceId);
          } catch {
            // The connection may close while the leave request is in flight.
          }
        }

        await connection.stop();
      })();
    };
  }, [token, workspaceId]);

  return status;
}
