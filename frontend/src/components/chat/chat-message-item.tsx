import {
  AlertCircle,
  Bot,
  CircleStop,
  UserRound,
} from "lucide-react";
import { memo } from "react";

import { CitationBadge } from "@/components/chat/citation-badge";
import { MarkdownRenderer } from "@/components/chat/markdown-renderer";
import { cn, formatDateTime } from "@/lib/utils";
import type { ChatMessage, Citation } from "@/types/chat.types";

function ChatMessageItemComponent({
  message,
  onCitationSelect,
}: {
  message: ChatMessage;
  onCitationSelect: (citation: Citation, index: number) => void;
}) {
  const isUser = message.role === "User";
  const isWaiting =
    !isUser && message.status === "streaming" && !message.content;

  return (
    <article
      className={cn(
        "flex gap-3 sm:gap-4",
        isUser ? "justify-end" : "justify-start",
      )}
    >
      {!isUser ? (
        <span className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-xl bg-blue-600 text-white shadow-sm shadow-blue-600/20">
          <Bot className="size-5" />
        </span>
      ) : null}

      <div
        className={cn(
          "min-w-0 max-w-[88%] sm:max-w-[82%]",
          isUser && "order-first",
        )}
      >
        <div
          className={cn(
            "rounded-2xl px-4 py-3.5 sm:px-5",
            isUser
              ? "rounded-tr-md bg-blue-50 text-slate-800 ring-1 ring-inset ring-blue-100"
              : "rounded-tl-md border border-slate-200 bg-white shadow-sm",
          )}
        >
          {isUser ? (
            <p className="whitespace-pre-wrap text-sm leading-7">
              {message.content}
            </p>
          ) : isWaiting ? (
            <div className="flex h-7 items-center gap-1.5" aria-label="Đang suy nghĩ">
              {[0, 1, 2].map((item) => (
                <span
                  className="size-2 animate-bounce rounded-full bg-blue-400"
                  key={item}
                  style={{ animationDelay: `${item * 120}ms` }}
                />
              ))}
            </div>
          ) : (
            <MarkdownRenderer content={message.content} />
          )}

          {!isUser && message.status === "streaming" && message.content ? (
            <span className="ml-1 inline-block h-4 w-0.5 animate-pulse bg-blue-500 align-middle" />
          ) : null}

          {!isUser && message.status === "stopped" ? (
            <p className="mt-3 flex items-center gap-1.5 border-t border-slate-100 pt-3 text-xs text-slate-500">
              <CircleStop className="size-3.5" />
              Đã dừng sinh câu trả lời
            </p>
          ) : null}

          {!isUser && message.status === "error" ? (
            <p className="mt-3 flex items-center gap-1.5 border-t border-rose-100 pt-3 text-xs text-rose-600">
              <AlertCircle className="size-3.5" />
              Câu trả lời bị gián đoạn
            </p>
          ) : null}
        </div>

        {!isUser && message.citations.length > 0 ? (
          <div className="mt-2.5 flex flex-wrap items-center gap-2">
            <span className="text-xs font-medium text-slate-400">Nguồn</span>
            {message.citations.map((citation, index) => (
              <CitationBadge
                citation={citation}
                index={index + 1}
                key={`${citation.chunkId}-${index}`}
                onSelect={onCitationSelect}
              />
            ))}
          </div>
        ) : null}

        <p
          className={cn(
            "mt-1.5 text-[11px] text-slate-400",
            isUser && "text-right",
          )}
        >
          {formatDateTime(message.createdAtUtc)}
        </p>
      </div>

      {isUser ? (
        <span className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-xl bg-slate-200 text-slate-600">
          <UserRound className="size-4.5" />
        </span>
      ) : null}
    </article>
  );
}

export const ChatMessageItem = memo(ChatMessageItemComponent);
