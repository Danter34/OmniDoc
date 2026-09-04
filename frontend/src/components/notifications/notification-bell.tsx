"use client";

import { Bell, CheckCheck, FileCheck2, MailPlus, X } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { useNotifications } from "@/hooks/use-notifications";
import { getErrorMessage } from "@/services/api-client";
import type { AppNotification } from "@/types/notification.types";

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
    function handlePointerDown(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, []);

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
          aria-haspopup="menu"
          aria-label={unreadCount > 0 ? `Thông báo, ${unreadCount} chưa đọc` : "Thông báo"}
          className="relative flex size-10 items-center justify-center rounded-xl text-slate-600 transition hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
          onClick={() => {
            const nextOpen = !open;
            setOpen(nextOpen);
            if (nextOpen) void refresh();
          }}
          type="button"
        >
          <Bell className="size-5" />
          {unreadCount > 0 ? (
            <span className="absolute right-0.5 top-0.5 flex min-w-4 items-center justify-center rounded-full bg-rose-500 px-1 text-[10px] font-bold leading-4 text-white ring-2 ring-white">
              {unreadCount > 9 ? "9+" : unreadCount}
            </span>
          ) : null}
        </button>

        {open ? (
          <div
            className="absolute right-0 top-[calc(100%+8px)] z-50 flex max-h-[min(560px,calc(100vh-88px))] w-[min(390px,calc(100vw-24px))] flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl shadow-slate-950/10"
            role="menu"
          >
            <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3.5">
              <div>
                <h2 className="font-semibold text-slate-900">Thông báo</h2>
                <p className="text-xs text-slate-500">
                  {unreadCount > 0 ? `${unreadCount} mục chưa đọc` : "Bạn đã xem tất cả"}
                </p>
              </div>
              {unreadCount > 0 ? (
                <button
                  className="flex items-center gap-1.5 rounded-lg px-2 py-1.5 text-xs font-medium text-blue-600 transition hover:bg-blue-50"
                  onClick={() => void handleMarkAll()}
                  type="button"
                >
                  <CheckCheck className="size-3.5" />
                  Đọc tất cả
                </button>
              ) : null}
            </div>

            {actionError || error ? (
              <div className="border-b border-rose-100 bg-rose-50 px-4 py-2 text-xs text-rose-700">
                {actionError ?? error}
              </div>
            ) : null}

            <div className="min-h-24 overflow-y-auto">
              {isLoading && notifications.length === 0 ? (
                <div className="space-y-3 p-4" aria-label="Đang tải thông báo">
                  {[0, 1, 2].map((item) => (
                    <div className="h-16 animate-pulse rounded-xl bg-slate-100" key={item} />
                  ))}
                </div>
              ) : notifications.length === 0 ? (
                <div className="flex flex-col items-center px-6 py-12 text-center">
                  <div className="flex size-11 items-center justify-center rounded-full bg-slate-100 text-slate-400">
                    <Bell className="size-5" />
                  </div>
                  <p className="mt-3 text-sm font-medium text-slate-700">Không có thông báo nào</p>
                  <p className="mt-1 text-xs text-slate-500">Thông báo mới sẽ xuất hiện tại đây.</p>
                </div>
              ) : (
                notifications.map((notification) => (
                  <button
                    className={`relative flex w-full gap-3 border-b border-slate-100 px-4 py-3.5 text-left transition last:border-b-0 hover:bg-slate-50 ${notification.isRead ? "bg-white" : "bg-blue-50/70"}`}
                    key={notification.id}
                    onClick={() => void handleNotificationClick(notification)}
                    role="menuitem"
                    type="button"
                  >
                    <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-full bg-blue-100 text-blue-600">
                      <NotificationIcon type={notification.type} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className={`block text-sm text-slate-900 ${notification.isRead ? "font-medium" : "font-semibold"}`}>
                        {notification.title}
                      </span>
                      <span className="mt-0.5 line-clamp-2 block text-xs leading-5 text-slate-600">
                        {notification.message}
                      </span>
                      <span className="mt-1 block text-[11px] text-slate-400">
                        {relativeTime(notification.createdAt)}
                      </span>
                    </span>
                    {!notification.isRead ? (
                      <span className="mt-2 size-2 shrink-0 rounded-full bg-blue-500" aria-label="Chưa đọc" />
                    ) : null}
                  </button>
                ))
              )}
            </div>
          </div>
        ) : null}
      </div>

      {toastNotification ? (
        <div className="fixed right-4 top-20 z-[70] flex w-[min(360px,calc(100vw-32px))] gap-3 rounded-2xl border border-blue-100 bg-white p-4 shadow-xl shadow-slate-950/10" role="status">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-blue-100 text-blue-600">
            <NotificationIcon type={toastNotification.type} />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold text-slate-900">{toastNotification.title}</p>
            <p className="mt-0.5 line-clamp-2 text-xs leading-5 text-slate-600">{toastNotification.message}</p>
          </div>
          <button aria-label="Đóng thông báo" className="self-start rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700" onClick={dismissToast} type="button">
            <X className="size-4" />
          </button>
        </div>
      ) : null}
    </>
  );
}
