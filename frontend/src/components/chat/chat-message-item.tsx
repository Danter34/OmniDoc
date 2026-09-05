import {
  AlertCircle,
  CircleStop,
  UserRound,
} from "lucide-react";
import Image from "next/image";
import { memo } from "react";

import {
  CitationBadge,
  getCitationKey,
} from "@/components/chat/citation-badge";
import { MarkdownRenderer } from "@/components/chat/markdown-renderer";
import { cn, formatDateTime } from "@/lib/utils";
import type { ChatMessage, Citation } from "@/types/chat.types";

function ChatMessageItemComponent({
  message,
  onCitationSelect,
  activeCitationKey,
}: {
  message: ChatMessage;
  onCitationSelect: (citation: Citation, index: number) => void;
  activeCitationKey: string | null;
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
        <Image
          alt="OmniDoc AI"
          className="mt-1 size-7 shrink-0 rounded-full border border-line-subtle shadow-[0_0_20px_var(--brand-icon-shadow)]"
          height={28}
          src="/images/logo-icon.png"
          width={28}
        />
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
            !isUser && message.status === "streaming" && "streaming-aura",
            isUser
              ? "rounded-tr-md border border-chat-user-line bg-chat-user text-content"
              : "rounded-tl-md border border-line-subtle bg-chat-assistant shadow-sm",
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
                  className="size-2 animate-bounce rounded-full bg-accent"
                  key={item}
                  style={{ animationDelay: `${item * 120}ms` }}
                />
              ))}
            </div>
          ) : (
            <MarkdownRenderer content={message.content} />
          )}

          {!isUser && message.status === "streaming" && message.content ? (
            <span className="ml-1 inline-block h-4 w-0.5 animate-pulse bg-accent align-middle" />
          ) : null}

          {!isUser && message.status === "stopped" ? (
            <p className="mt-3 flex items-center gap-1.5 border-t border-line-subtle pt-3 text-xs text-muted">
              <CircleStop className="size-3.5" />
              Đã dừng sinh câu trả lời
            </p>
          ) : null}

          {!isUser && message.status === "error" ? (
            <p className="mt-3 flex items-center gap-1.5 border-t border-danger pt-3 text-xs text-danger">
              <AlertCircle className="size-3.5" />
              Câu trả lời bị gián đoạn
            </p>
          ) : null}
        </div>

        {!isUser && message.citations.length > 0 ? (
          <div className="mt-2.5 flex flex-wrap items-center gap-2">
            <span className="text-xs font-medium text-muted">Nguồn</span>
            {message.citations.map((citation, index) => (
              <CitationBadge
                citation={citation}
                index={index + 1}
                key={`${citation.chunkId}-${index}`}
                active={getCitationKey(citation) === activeCitationKey}
                onSelect={onCitationSelect}
              />
            ))}
          </div>
        ) : null}

        <p
          className={cn(
            "mt-1.5 text-[11px] text-muted",
            isUser && "text-right",
          )}
        >
          {formatDateTime(message.createdAtUtc)}
        </p>
      </div>

      {isUser ? (
        <span className="mt-1 flex size-9 shrink-0 items-center justify-center rounded-xl bg-surface-tertiary text-content-secondary">
          <UserRound className="size-4.5" />
        </span>
      ) : null}
    </article>
  );
}

export const ChatMessageItem = memo(ChatMessageItemComponent);
