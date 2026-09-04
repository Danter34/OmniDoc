"use client";

import {
  AlertCircle,
  MessageSquareText,
  PanelLeftClose,
  PanelLeftOpen,
  Plus,
  Trash2,
  X,
} from "lucide-react";
import {
  memo,
  useEffect,
  useEffectEvent,
  useRef,
  useState,
} from "react";

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

const DRAWER_FOCUSABLE_SELECTOR = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])',
].join(",");

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
  collapsed = false,
  onCollapsedToggle,
  mobile,
}: Omit<ConversationSidebarProps, "mobileOpen"> & {
  collapsed?: boolean;
  onCollapsedToggle?: () => void;
  mobile?: boolean;
}) {
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
    <div className="flex h-full min-h-0 flex-col bg-surface/55">
      <div
        className={cn(
          "flex items-center border-b border-line-subtle py-3.5",
          collapsed ? "justify-center px-2" : "justify-between px-4",
        )}
      >
        {!collapsed ? (
          <div>
            <h2 className="text-sm font-semibold text-content">Hội thoại</h2>
            <p className="mt-0.5 text-xs text-muted">
              {conversations.length} cuộc trò chuyện
            </p>
          </div>
        ) : null}
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
          <button
            aria-label={collapsed ? "Mở rộng danh sách hội thoại" : "Thu gọn danh sách hội thoại"}
            className="flex size-11 items-center justify-center rounded-xl text-muted transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
            onClick={onCollapsedToggle}
            title={collapsed ? "Mở rộng danh sách hội thoại" : "Thu gọn danh sách hội thoại"}
            type="button"
          >
            {collapsed ? (
              <PanelLeftOpen className="size-4" />
            ) : (
              <PanelLeftClose className="size-4" />
            )}
          </button>
        )}
      </div>

      <div className={cn("p-3", collapsed && "px-2")}>
        <Button
          aria-label={collapsed ? "Tạo cuộc trò chuyện mới" : undefined}
          className={cn("w-full", collapsed && "size-11 px-0")}
          disabled={disabled || isCreating}
          icon={isCreating ? <Spinner /> : <Plus className="size-4" />}
          onClick={() => void createConversation()}
        >
          {!collapsed && (isCreating ? "Đang tạo..." : "Cuộc trò chuyện mới")}
        </Button>
      </div>

      {(error || actionError) && !isLoading ? (
        <div
          className={cn(
            "mx-3 mb-2 flex items-start gap-2 rounded-xl border border-danger bg-danger-subtle p-3 text-xs leading-5 text-danger",
            collapsed && "mx-2 justify-center px-2",
          )}
          title={collapsed ? (actionError ?? error ?? undefined) : undefined}
        >
          <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
          {!collapsed && (actionError || error)}
        </div>
      ) : null}

      <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 py-10 text-xs text-muted">
            <Spinner className="text-accent" />
            {!collapsed && "Đang tải hội thoại..."}
          </div>
        ) : conversations.length === 0 ? (
          <div className={cn("py-10 text-center", collapsed ? "px-1" : "px-4")}>
            <span className="mx-auto flex size-11 items-center justify-center rounded-2xl bg-surface text-muted shadow-sm ring-1 ring-line-subtle">
              <MessageSquareText className="size-5" />
            </span>
            {!collapsed ? (
              <>
                <p className="mt-3 text-sm font-medium text-content-secondary">
                  Chưa có hội thoại
                </p>
                <p className="mt-1 text-xs leading-5 text-muted">
                  Đặt câu hỏi đầu tiên hoặc tạo một cuộc trò chuyện mới.
                </p>
              </>
            ) : null}
          </div>
        ) : (
          <div className="space-y-1">
            {conversations.map((conversation) => {
              const active = conversation.id === activeConversationId;

              return (
                <div
                  className={cn(
                    "group flex w-full items-start gap-1 rounded-xl transition-[background-color,color,box-shadow]",
                    collapsed ? "justify-center" : "pr-2",
                    active
                      ? "active-gradient-item"
                      : "text-content-secondary hover:bg-surface hover:shadow-sm",
                  )}
                  key={conversation.id}
                >
                  <button
                    aria-label={collapsed ? conversation.title : undefined}
                    aria-current={active ? "true" : undefined}
                    className={cn(
                      "flex min-w-0 items-start gap-2 py-3 text-left",
                      collapsed ? "size-11 justify-center px-0" : "flex-1 px-3",
                    )}
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
                        active
                          ? "text-sidebar-active-content drop-shadow-[0_0_5px_var(--sidebar-icon-glow)]"
                          : "text-muted",
                      )}
                    />
                    {!collapsed ? <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">
                        {conversation.title}
                      </span>
                      <span
                        className={cn(
                          "mt-1 block text-[11px]",
                          active ? "text-sidebar-active-content" : "text-muted",
                        )}
                      >
                        {formatConversationDate(conversation.lastActivityAtUtc)}
                      </span>
                    </span> : null}
                  </button>
                  {!collapsed ? <button
                    aria-label="Xóa hội thoại"
                    className={cn(
                      "mt-2.5 flex size-7 shrink-0 items-center justify-center rounded-lg text-muted opacity-100 transition hover:bg-danger-subtle hover:text-danger focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring lg:opacity-0 lg:group-hover:opacity-100",
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
                  </button> : null}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

function ConversationSidebarComponent(props: ConversationSidebarProps) {
  const [collapsed, setCollapsed] = useState(false);
  const mobileDrawerRef = useRef<HTMLElement>(null);
  const closeMobileDrawer = useEffectEvent(() => props.onMobileClose());

  useEffect(() => {
    const drawer = mobileDrawerRef.current;
    if (!props.mobileOpen || !drawer) {
      return;
    }

    const opener =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const focusFrame = window.requestAnimationFrame(() => {
      const firstFocusable =
        drawer.querySelector<HTMLElement>(DRAWER_FOCUSABLE_SELECTOR);
      (firstFocusable ?? drawer).focus();
    });

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        closeMobileDrawer();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusableElements = Array.from(
        drawer.querySelectorAll<HTMLElement>(DRAWER_FOCUSABLE_SELECTOR),
      );

      if (focusableElements.length === 0) {
        event.preventDefault();
        drawer.focus();
        return;
      }

      const firstFocusable = focusableElements[0];
      const lastFocusable = focusableElements.at(-1)!;
      const activeElement = document.activeElement;

      if (
        event.shiftKey &&
        (activeElement === firstFocusable || !drawer.contains(activeElement))
      ) {
        event.preventDefault();
        lastFocusable.focus();
      } else if (
        !event.shiftKey &&
        (activeElement === lastFocusable || !drawer.contains(activeElement))
      ) {
        event.preventDefault();
        firstFocusable.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = previousOverflow;
      if (opener?.isConnected) {
        opener.focus();
      }
    };
  }, [props.mobileOpen]);

  return (
    <>
      <aside
        className={cn(
          "glass-panel hidden min-h-0 shrink-0 overflow-hidden rounded-none border-y-0 border-l-0 transition-[width] duration-300 ease-out lg:block",
          collapsed ? "w-[4.5rem]" : "w-72",
        )}
      >
        <SidebarContent
          {...props}
          collapsed={collapsed}
          onCollapsedToggle={() => setCollapsed((current) => !current)}
        />
      </aside>

      {props.mobileOpen ? (
        <div
          className="fixed inset-0 z-40 bg-overlay backdrop-blur-[1px] lg:hidden"
          onPointerDown={(event) => {
            if (event.currentTarget === event.target) {
              props.onMobileClose();
            }
          }}
          role="presentation"
        >
          <aside
            aria-label="Danh sách hội thoại"
            aria-modal="true"
            className="glass-panel h-full w-[min(21rem,88vw)] rounded-none border-y-0 border-l-0 shadow-2xl"
            ref={mobileDrawerRef}
            role="dialog"
            tabIndex={-1}
          >
            <SidebarContent {...props} mobile />
          </aside>
        </div>
      ) : null}
    </>
  );
}

export const ConversationSidebar = memo(ConversationSidebarComponent);
