"use client";

import {
  AlertCircle,
  MessageSquareText,
  PanelLeftClose,
  Plus,
  Trash2,
  X,
} from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import type { Conversation } from "@/types/chat.types";

interface ConversationSidebarProps {
  conversations: Conversation[];
  activeConversationId: string | null;
  isLoading: boolean;
  error: string | null;
  mobileOpen: boolean;
  disabled: boolean;
  onMobileClose: () => void;
  onSelect: (conversationId: string) => void;
  onCreate: () => Promise<void>;
  onDelete: (conversationId: string) => Promise<void>;
}

function formatConversationDate(value: string) {
  const date = new Date(value);
  const now = new Date();
  const sameDay = date.toDateString() === now.toDateString();

  return new Intl.DateTimeFormat("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    ...(sameDay
      ? {}
      : {
          day: "2-digit",
          month: "2-digit",
        }),
  }).format(date);
}

function SidebarContent({
  conversations,
  activeConversationId,
  isLoading,
  error,
  disabled,
  onSelect,
  onCreate,
  onDelete,
  onMobileClose,
  mobile,
}: Omit<ConversationSidebarProps, "mobileOpen"> & { mobile?: boolean }) {
  const [isCreating, setIsCreating] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  async function createConversation() {
    setIsCreating(true);
    setActionError(null);

    try {
      await onCreate();
      onMobileClose();
    } catch (requestError) {
      setActionError(getErrorMessage(requestError));
    } finally {
      setIsCreating(false);
    }
  }

  async function deleteConversation(
    event: React.MouseEvent,
    conversation: Conversation,
  ) {
    event.stopPropagation();

    if (
      !window.confirm(
        `Xóa cuộc trò chuyện “${conversation.title}” và toàn bộ tin nhắn?`,
      )
    ) {
      return;
    }

    setDeletingId(conversation.id);
    setActionError(null);

    try {
      await onDelete(conversation.id);
    } catch (requestError) {
      setActionError(getErrorMessage(requestError));
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <div className="flex h-full min-h-0 flex-col bg-slate-50/80">
      <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3.5">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Hội thoại</h2>
          <p className="mt-0.5 text-xs text-slate-500">
            {conversations.length} cuộc trò chuyện
          </p>
        </div>
        {mobile ? (
          <Button
            aria-label="Đóng danh sách hội thoại"
            className="size-9 px-0"
            onClick={onMobileClose}
            variant="ghost"
          >
            <X className="size-5" />
          </Button>
        ) : (
          <PanelLeftClose className="size-4 text-slate-300" />
        )}
      </div>

      <div className="p-3">
        <Button
          className="w-full"
          disabled={disabled || isCreating}
          icon={isCreating ? <Spinner /> : <Plus className="size-4" />}
          onClick={() => void createConversation()}
        >
          {isCreating ? "Đang tạo..." : "Cuộc trò chuyện mới"}
        </Button>
      </div>

      {(error || actionError) && !isLoading ? (
        <div className="mx-3 mb-2 flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs leading-5 text-rose-700">
          <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
          {actionError || error}
        </div>
      ) : null}

      <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 py-10 text-xs text-slate-500">
            <Spinner className="text-blue-600" />
            Đang tải hội thoại...
          </div>
        ) : conversations.length === 0 ? (
          <div className="px-4 py-10 text-center">
            <span className="mx-auto flex size-11 items-center justify-center rounded-2xl bg-white text-slate-400 shadow-sm ring-1 ring-slate-200">
              <MessageSquareText className="size-5" />
            </span>
            <p className="mt-3 text-sm font-medium text-slate-700">
              Chưa có hội thoại
            </p>
            <p className="mt-1 text-xs leading-5 text-slate-500">
              Đặt câu hỏi đầu tiên hoặc tạo một cuộc trò chuyện mới.
            </p>
          </div>
        ) : (
          <div className="space-y-1">
            {conversations.map((conversation) => {
              const active = conversation.id === activeConversationId;

              return (
                <div
                  className={cn(
                    "group flex w-full items-start gap-1 rounded-xl pr-2 transition",
                    active
                      ? "bg-blue-100/80 text-blue-900"
                      : "text-slate-700 hover:bg-white hover:shadow-sm",
                  )}
                  key={conversation.id}
                >
                  <button
                    className="flex min-w-0 flex-1 items-start gap-2 px-3 py-3 text-left"
                    disabled={disabled}
                    onClick={() => {
                      onSelect(conversation.id);
                      onMobileClose();
                    }}
                    type="button"
                  >
                    <MessageSquareText
                      className={cn(
                        "mt-0.5 size-4 shrink-0",
                        active ? "text-blue-600" : "text-slate-400",
                      )}
                    />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">
                        {conversation.title}
                      </span>
                      <span
                        className={cn(
                          "mt-1 block text-[11px]",
                          active ? "text-blue-600" : "text-slate-400",
                        )}
                      >
                        {formatConversationDate(conversation.lastActivityAtUtc)}
                      </span>
                    </span>
                  </button>
                  <button
                    aria-label="Xóa hội thoại"
                    className={cn(
                      "mt-2.5 flex size-7 shrink-0 items-center justify-center rounded-lg text-slate-400 opacity-100 transition hover:bg-rose-50 hover:text-rose-600 focus-visible:opacity-100 lg:opacity-0 lg:group-hover:opacity-100",
                      deletingId === conversation.id && "opacity-100",
                    )}
                    disabled={disabled || deletingId === conversation.id}
                    onClick={(event) =>
                      void deleteConversation(event, conversation)
                    }
                    type="button"
                  >
                    {deletingId === conversation.id ? (
                      <Spinner className="size-3.5" />
                    ) : (
                      <Trash2 className="size-3.5" />
                    )}
                  </button>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

export function ConversationSidebar(props: ConversationSidebarProps) {
  return (
    <>
      <aside className="hidden min-h-0 border-r border-slate-200 lg:block">
        <SidebarContent {...props} />
      </aside>

      {props.mobileOpen ? (
        <div
          className="fixed inset-0 z-40 bg-slate-950/35 backdrop-blur-[1px] lg:hidden"
          onMouseDown={(event) => {
            if (event.currentTarget === event.target) {
              props.onMobileClose();
            }
          }}
          role="presentation"
        >
          <aside className="h-full w-[min(21rem,88vw)] border-r border-slate-200 bg-white shadow-2xl">
            <SidebarContent {...props} mobile />
          </aside>
        </div>
      ) : null}
    </>
  );
}
