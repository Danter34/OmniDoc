"use client";

import { useCallback, useEffect, useMemo, useState } from "react";

import { getErrorMessage } from "@/services/api-client";
import { conversationService } from "@/services/conversation.service";
import type { Conversation } from "@/types/chat.types";

function sortConversations(items: Conversation[]) {
  return [...items].sort(
    (left, right) =>
      new Date(right.lastActivityAtUtc).getTime() -
      new Date(left.lastActivityAtUtc).getTime(),
  );
}

export function useConversations(workspaceId: string) {
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [activeConversationId, setActiveConversationId] = useState<
    string | null
  >(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const storeConversations = useCallback((items: Conversation[]) => {
    const sorted = sortConversations(items);
    setConversations(sorted);
    setActiveConversationId((current) =>
      current && sorted.some((item) => item.id === current)
        ? current
        : (sorted[0]?.id ?? null),
    );
  }, []);

  const refreshConversations = useCallback(async () => {
    setError(null);

    try {
      storeConversations(await conversationService.getAll(workspaceId));
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsLoading(false);
    }
  }, [storeConversations, workspaceId]);

  useEffect(() => {
    const controller = new AbortController();

    conversationService
      .getAll(workspaceId, controller.signal)
      .then((items) => {
        storeConversations(items);
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
  }, [storeConversations, workspaceId]);

  const createConversation = useCallback(
    async (title: string) => {
      const created = await conversationService.create(workspaceId, { title });
      setConversations((current) =>
        sortConversations([
          created,
          ...current.filter((item) => item.id !== created.id),
        ]),
      );
      setActiveConversationId(created.id);
      return created;
    },
    [workspaceId],
  );

  const deleteConversation = useCallback(
    async (conversationId: string) => {
      await conversationService.delete(workspaceId, conversationId);
      const remaining = conversations.filter(
        (item) => item.id !== conversationId,
      );
      setConversations(remaining);

      if (activeConversationId === conversationId) {
        setActiveConversationId(remaining[0]?.id ?? null);
      }
    },
    [activeConversationId, conversations, workspaceId],
  );

  const activeConversation = useMemo(
    () =>
      conversations.find((item) => item.id === activeConversationId) ?? null,
    [activeConversationId, conversations],
  );

  return {
    conversations,
    activeConversation,
    activeConversationId,
    isLoading,
    error,
    selectConversation: setActiveConversationId,
    createConversation,
    deleteConversation,
    refreshConversations,
  };
}
