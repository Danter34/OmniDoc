"use client";

import { Bell, CheckCheck, FileCheck2, MailPlus, X } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { useNotifications } from "@/hooks/use-notifications";
import { getErrorMessage } from "@/services/api-client";
import type { AppNotification } from "@/types/notification.types";

const FOCUSABLE_SELECTOR = [
  "button:not([disabled])",
  "[href]",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])',
].join(",");

function relativeTime(value: string) {
  const elapsedSeconds = Math.max(
    0,
    Math.floor((Date.now() - new Date(value).getTime()) / 1000),
  );

  if (elapsedSeconds < 60) return "vừa xong";
  if (elapsedSeconds < 3_600) return `${Math.floor(elapsedSeconds / 60)} phút trước`;
  if (elapsedSeconds < 86_400) return `${Math.floor(elapsedSeconds / 3_600)} giờ trước`;
  return `${Math.floor(elapsedSeconds / 86_400)} ngày trước`;
}

function NotificationIcon({ type }: { type: AppNotification["type"] }) {
  if (type === "WorkspaceInvitation") return <MailPlus className="size-4" />;
  if (type === "DocumentProcessed") return <FileCheck2 className="size-4" />;
  return <Bell className="size-4" />;
}

export function NotificationBell() {
  const router = useRouter();
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const {
    notifications,
    unreadCount,
    isLoading,
    error,
    toastNotification,
    dismissToast,
    markAsRead,
    markAllAsRead,
    refresh,
  } = useNotifications();

  useEffect(() => {
    if (!open) return;

    const dialog = dialogRef.current;
    const initialFocusFrame = window.requestAnimationFrame(() => {
      const firstFocusable = dialog?.querySelector<HTMLElement>(
        FOCUSABLE_SELECTOR,
      );
      (firstFocusable ?? dialog)?.focus();
    });

    function handlePointerDown(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
        return;
      }

      if (event.key !== "Tab" || !dialogRef.current) return;

      const focusableElements = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      );

      if (focusableElements.length === 0) {
        event.preventDefault();
        dialogRef.current.focus();
        return;
      }

      const firstFocusable = focusableElements[0];
      const lastFocusable = focusableElements.at(-1);
      const activeElement = document.activeElement;

      if (
        event.shiftKey &&
        (activeElement === firstFocusable || activeElement === dialogRef.current)
      ) {
        event.preventDefault();
        lastFocusable?.focus();
      } else if (
        !event.shiftKey &&
        (activeElement === lastFocusable ||
          !dialogRef.current.contains(activeElement))
      ) {
        event.preventDefault();
        firstFocusable.focus();
      }
    }

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      window.cancelAnimationFrame(initialFocusFrame);
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  async function handleNotificationClick(notification: AppNotification) {
    setActionError(null);
    try {
      if (!notification.isRead) await markAsRead(notification.id);
      setOpen(false);
      if (notification.actionUrl) router.push(notification.actionUrl);
    } catch (clickError) {
      setActionError(getErrorMessage(clickError));
    }
  }

  async function handleMarkAll() {
    setActionError(null);
    try {
      await markAllAsRead();
    } catch (markError) {
      setActionError(getErrorMessage(markError));
    }
  }

  return (
    <>
      <div className="relative" ref={containerRef}>
        <button
          aria-expanded={open}
          aria-haspopup="dialog"
          aria-label={unreadCount > 0 ? `Thông báo, ${unreadCount} chưa đọc` : "Thông báo"}
          className="relative flex size-11 items-center justify-center rounded-xl text-content-secondary transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
          onClick={() => {
            const nextOpen = !open;
            setOpen(nextOpen);
            if (nextOpen) void refresh();
          }}
          ref={triggerRef}
          type="button"
        >
          <Bell className="size-5" />
          {unreadCount > 0 ? (
            <span className="absolute right-0 top-0 flex min-w-4 items-center justify-center rounded-full bg-notification-badge px-1 text-[10px] font-bold leading-4 text-notification-badge-content ring-2 ring-surface">
              {unreadCount > 9 ? "9+" : unreadCount}
            </span>
          ) : null}
        </button>

        {open ? (
          <div
            className="glass-panel absolute right-0 top-[calc(100%+8px)] z-50 flex max-h-[min(560px,calc(100vh-88px))] w-[min(390px,calc(100vw-24px))] flex-col overflow-hidden rounded-2xl"
            aria-label="Trung tâm thông báo"
            aria-modal="true"
            ref={dialogRef}
            role="dialog"
            tabIndex={-1}
          >
            <div className="flex items-center justify-between border-b border-line-subtle px-4 py-3.5">
              <div>
                <h2 className="font-semibold text-content">Thông báo</h2>
                <p className="text-xs text-muted">
                  {unreadCount > 0 ? `${unreadCount} mục chưa đọc` : "Bạn đã xem tất cả"}
                </p>
              </div>
              {unreadCount > 0 ? (
                <button
                  className="flex min-h-11 items-center gap-1.5 rounded-lg px-2 text-xs font-medium text-accent transition-colors hover:bg-info-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                  onClick={() => void handleMarkAll()}
                  type="button"
                >
                  <CheckCheck className="size-3.5" />
                  Đọc tất cả
                </button>
              ) : null}
            </div>

            {actionError || error ? (
              <div className="border-b border-danger bg-danger-subtle px-4 py-2 text-xs text-danger">
                {actionError ?? error}
              </div>
            ) : null}

            <div className="min-h-24 overflow-y-auto">
              {isLoading && notifications.length === 0 ? (
                <div className="space-y-3 p-4" aria-label="Đang tải thông báo">
                  {[0, 1, 2].map((item) => (
                    <div className="h-16 animate-pulse rounded-xl bg-surface-tertiary" key={item} />
                  ))}
                </div>
              ) : notifications.length === 0 ? (
                <div className="flex flex-col items-center px-6 py-12 text-center">
                  <div className="flex size-11 items-center justify-center rounded-full bg-surface-tertiary text-muted">
                    <Bell className="size-5" />
                  </div>
                  <p className="mt-3 text-sm font-medium text-content-secondary">Không có thông báo nào</p>
                  <p className="mt-1 text-xs text-muted">Thông báo mới sẽ xuất hiện tại đây.</p>
                </div>
              ) : (
                notifications.map((notification) => (
                  <button
                    className={`relative flex w-full gap-3 border-b border-line-subtle px-4 py-3.5 text-left transition-colors last:border-b-0 hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-focus-ring ${notification.isRead ? "bg-surface" : "bg-notification-unread"}`}
                    key={notification.id}
                    onClick={() => void handleNotificationClick(notification)}
                    type="button"
                  >
                    <span className="sr-only">
                      {notification.isRead ? "Đã đọc. " : "Chưa đọc. "}
                    </span>
                    <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-full bg-info-subtle text-accent">
                      <NotificationIcon type={notification.type} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className={`block text-sm text-content ${notification.isRead ? "font-medium" : "font-semibold"}`}>
                        {notification.title}
                      </span>
                      <span className="mt-0.5 line-clamp-2 block text-xs leading-5 text-content-secondary">
                        {notification.message}
                      </span>
                      <span className="mt-1 block text-[11px] text-muted">
                        {relativeTime(notification.createdAt)}
                      </span>
                    </span>
                    {!notification.isRead ? (
                      <span
                        aria-hidden="true"
                        className="mt-2 size-2 shrink-0 rounded-full bg-notification-unread-dot"
                      />
                    ) : null}
                  </button>
                ))
              )}
            </div>
          </div>
        ) : null}
      </div>

      {toastNotification ? (
        <div className="glass-panel fixed right-4 top-20 z-[70] flex w-[min(360px,calc(100vw-32px))] gap-3 rounded-2xl p-4" role="status">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-info-subtle text-accent">
            <NotificationIcon type={toastNotification.type} />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-content">{toastNotification.title}</p>
            <p className="mt-0.5 line-clamp-2 text-xs leading-5 text-content-secondary">{toastNotification.message}</p>
          </div>
          <button aria-label="Đóng thông báo" className="flex size-11 shrink-0 items-center justify-center self-start rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring" onClick={dismissToast} type="button">
            <X className="size-4" />
          </button>
        </div>
      ) : null}
    </>
  );
}
