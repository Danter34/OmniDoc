"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { getErrorMessage } from "@/services/api-client";
import { chatService } from "@/services/chat.service";
import type {
  ChatMessage,
  ChatStreamEvent,
} from "@/types/chat.types";

interface UseChatStreamOptions {
  workspaceId: string;
  conversationId: string | null;
  onConversationResolved?: (conversationId: string) => void;
  onSettled?: () => void;
}

interface ParsedSseFrame {
  eventName?: string;
  data: string;
}

function parseFrame(frame: string): ParsedSseFrame | null {
  let eventName: string | undefined;
  const dataLines: string[] = [];

  for (const line of frame.split(/\r?\n/)) {
    if (!line || line.startsWith(":")) {
      continue;
    }

    if (line.startsWith("event:")) {
      eventName = line.slice("event:".length).trim();
    } else if (line.startsWith("data:")) {
      dataLines.push(line.slice("data:".length).trimStart());
    }
  }

  if (dataLines.length === 0) {
    return null;
  }

  return { eventName, data: dataLines.join("\n") };
}

function parseStreamEvent(frame: ParsedSseFrame): ChatStreamEvent {
  if (frame.data === "[DONE]") {
    return { type: "done" };
  }

  try {
    const payload = JSON.parse(frame.data) as Partial<ChatStreamEvent>;

    if (payload.type) {
      return payload as ChatStreamEvent;
    }

    if (frame.eventName) {
      return {
        ...(payload as Omit<ChatStreamEvent, "type">),
        type: frame.eventName as ChatStreamEvent["type"],
      };
    }
  } catch {
    if (frame.eventName === "token") {
      return { type: "token", content: frame.data };
    }
  }

  throw new Error("Nhận được SSE event không hợp lệ từ máy chủ.");
}

function createLocalMessage(
  role: ChatMessage["role"],
  conversationId: string,
  content: string,
  status: ChatMessage["status"],
): ChatMessage {
  return {
    id: crypto.randomUUID(),
    conversationId,
    role,
    content,
    createdAtUtc: new Date().toISOString(),
    citations: [],
    status,
  };
}

export function useChatStream({
  workspaceId,
  conversationId,
  onConversationResolved,
  onSettled,
}: UseChatStreamOptions) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const pendingTextRef = useRef("");
  const assistantMessageIdRef = useRef<string | null>(null);
  const onConversationResolvedRef = useRef(onConversationResolved);
  const onSettledRef = useRef(onSettled);

  useEffect(() => {
    onConversationResolvedRef.current = onConversationResolved;
    onSettledRef.current = onSettled;
  }, [onConversationResolved, onSettled]);

  const flushPendingText = useCallback(() => {
    if (!pendingTextRef.current || !assistantMessageIdRef.current) {
      return;
    }

    const text = pendingTextRef.current;
    const assistantId = assistantMessageIdRef.current;
    pendingTextRef.current = "";
    animationFrameRef.current = null;
    setMessages((current) =>
      current.map((message) =>
        message.id === assistantId
          ? { ...message, content: message.content + text }
          : message,
      ),
    );
  }, []);

  const scheduleTextFlush = useCallback(() => {
    if (animationFrameRef.current === null) {
      animationFrameRef.current =
        window.requestAnimationFrame(flushPendingText);
    }
  }, [flushPendingText]);

  const updateAssistant = useCallback(
    (updater: (message: ChatMessage) => ChatMessage) => {
      const assistantId = assistantMessageIdRef.current;

      if (!assistantId) {
        return;
      }

      setMessages((current) =>
        current.map((message) =>
          message.id === assistantId ? updater(message) : message,
        ),
      );
    },
    [],
  );

  const replaceMessages = useCallback((items: ChatMessage[]) => {
    setMessages(
      items.map((message) => ({
        ...message,
        status: "complete",
      })),
    );
    setError(null);
  }, []);

  const sendMessage = useCallback(
    async (message: string) => {
      const normalizedMessage = message.trim();

      if (!normalizedMessage || abortControllerRef.current) {
        return;
      }

      const temporaryConversationId =
        conversationId ?? `pending-${crypto.randomUUID()}`;
      const userMessage = createLocalMessage(
        "User",
        temporaryConversationId,
        normalizedMessage,
        "complete",
      );
      const assistantMessage = createLocalMessage(
        "Assistant",
        temporaryConversationId,
        "",
        "streaming",
      );
      const controller = new AbortController();
      abortControllerRef.current = controller;
      assistantMessageIdRef.current = assistantMessage.id;
      pendingTextRef.current = "";
      setError(null);
      setIsStreaming(true);
      setMessages((current) => [...current, userMessage, assistantMessage]);

      let streamFinished = false;
      let reader: ReadableStreamDefaultReader<Uint8Array> | null = null;

      try {
        const response = await chatService.stream(
          workspaceId,
          {
            conversationId,
            message: normalizedMessage,
          },
          controller.signal,
        );
        reader = response.body!.getReader();
        const decoder = new TextDecoder();
        let buffer = "";

        while (!streamFinished) {
          const { done, value } = await reader.read();
          buffer += decoder.decode(value, { stream: !done });
          const frames = buffer.split(/\r?\n\r?\n/);
          buffer = frames.pop() ?? "";

          for (const rawFrame of frames) {
            const parsedFrame = parseFrame(rawFrame);

            if (!parsedFrame) {
              continue;
            }

            const streamEvent = parseStreamEvent(parsedFrame);

            if (streamEvent.conversationId) {
              onConversationResolvedRef.current?.(
                streamEvent.conversationId,
              );
            }

            if (streamEvent.type === "token") {
              pendingTextRef.current += streamEvent.content ?? "";
              scheduleTextFlush();
            } else if (
              streamEvent.type === "citation" &&
              streamEvent.citation
            ) {
              flushPendingText();
              const citation = streamEvent.citation;
              updateAssistant((current) => ({
                ...current,
                citations: current.citations.some(
                  (item) => item.chunkId === citation.chunkId,
                )
                  ? current.citations
                  : [...current.citations, citation],
              }));
            } else if (streamEvent.type === "done") {
              flushPendingText();
              updateAssistant((current) => ({
                ...current,
                id: streamEvent.messageId ?? current.id,
                status: "complete",
              }));
              streamFinished = true;
              break;
            } else if (streamEvent.type === "error") {
              throw new Error(
                streamEvent.content || "Luồng trả lời đã gặp lỗi.",
              );
            }
          }

          if (done) {
            break;
          }
        }

        if (!streamFinished) {
          flushPendingText();
          updateAssistant((current) => ({
            ...current,
            status: "complete",
          }));
        }
      } catch (streamError) {
        flushPendingText();

        if (
          streamError instanceof DOMException &&
          streamError.name === "AbortError"
        ) {
          updateAssistant((current) => ({
            ...current,
            status: "stopped",
          }));
        } else {
          const messageText = getErrorMessage(streamError);
          setError(messageText);
          updateAssistant((current) => ({
            ...current,
            status: "error",
          }));
        }
      } finally {
        if (reader) {
          try {
            await reader.cancel();
          } catch {
            // The reader is already closed after a normal done frame.
          }
        }

        if (animationFrameRef.current !== null) {
          window.cancelAnimationFrame(animationFrameRef.current);
          animationFrameRef.current = null;
          flushPendingText();
        }

        abortControllerRef.current = null;
        assistantMessageIdRef.current = null;
        setIsStreaming(false);
        onSettledRef.current?.();
      }
    },
    [
      conversationId,
      flushPendingText,
      scheduleTextFlush,
      updateAssistant,
      workspaceId,
    ],
  );

  const stopGenerating = useCallback(() => {
    abortControllerRef.current?.abort();
  }, []);

  useEffect(() => {
    return () => {
      abortControllerRef.current?.abort();

      if (animationFrameRef.current !== null) {
        window.cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [workspaceId]);

  return {
    messages,
    isStreaming,
    error,
    sendMessage,
    stopGenerating,
    replaceMessages,
  };
}
