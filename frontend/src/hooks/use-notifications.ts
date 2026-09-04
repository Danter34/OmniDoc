"use client";

import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { useCallback, useEffect, useRef, useState } from "react";

import { useAuth } from "@/hooks/use-auth";
import { getErrorMessage, getRealtimeUrl } from "@/services/api-client";
import { notificationService } from "@/services/notification.service";
import type { AppNotification } from "@/types/notification.types";

const RECEIVE_NOTIFICATION_EVENT = "ReceiveNotification";

export function useNotifications() {
  const { token } = useAuth();
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [toastNotification, setToastNotification] =
    useState<AppNotification | null>(null);
  const notificationIdsRef = useRef(new Set<string>());

  const load = useCallback(async () => {
    if (!token) {
      setNotifications([]);
      setUnreadCount(0);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    try {
      const [page, unread] = await Promise.all([
        notificationService.getAll(),
        notificationService.getUnreadCount(),
      ]);
      notificationIdsRef.current = new Set(page.items.map((item) => item.id));
      setNotifications(page.items);
      setUnreadCount(unread.count);
      setError(null);
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setIsLoading(false);
    }
  }, [token]);

  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  }, [load]);

  useEffect(() => {
    if (!token) {
      return;
    }

    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(getRealtimeUrl("/hubs/notifications"), {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
      .configureLogging(
        process.env.NODE_ENV === "development"
          ? LogLevel.Warning
          : LogLevel.Error,
      )
      .build();

    const handleNotification = (notification: AppNotification) => {
      if (disposed || !notification?.id) {
        return;
      }

      if (notificationIdsRef.current.has(notification.id)) return;
      notificationIdsRef.current.add(notification.id);
      setNotifications((current) => [notification, ...current].slice(0, 20));

      if (!notification.isRead) {
        setUnreadCount((count) => count + 1);
        setToastNotification(notification);
      }
    };

    connection.on(RECEIVE_NOTIFICATION_EVENT, handleNotification);
    connection.onreconnected(() => {
      if (!disposed) {
        void loadRef.current();
      }
    });

    async function startConnection() {
      try {
        await connection.start();

        if (disposed) {
          await connection.stop();
          return;
        }

        await loadRef.current();
      } catch {
        // REST remains authoritative and automatic reconnect handles transient loss.
        if (!disposed) void loadRef.current();
      }
    }

    let startTimer: number | null = window.setTimeout(() => {
      startTimer = null;
      void startConnection();
    }, 0);

    return () => {
      disposed = true;

      if (startTimer !== null) {
        window.clearTimeout(startTimer);
        startTimer = null;
      }

      connection.off(RECEIVE_NOTIFICATION_EVENT, handleNotification);

      if (connection.state !== HubConnectionState.Connecting) {
        void connection.stop();
      }
    };
  }, [token]);

  useEffect(() => {
    if (!toastNotification) {
      return;
    }

    const timer = window.setTimeout(() => setToastNotification(null), 5_000);
    return () => window.clearTimeout(timer);
  }, [toastNotification]);

  const markAsRead = useCallback(async (id: string) => {
    const currentItem = notifications.find((item) => item.id === id);
    const updated = await notificationService.markAsRead(id);
    setNotifications((current) =>
      current.map((item) => (item.id === id ? updated : item)),
    );
    if (currentItem && !currentItem.isRead) {
      setUnreadCount((current) => Math.max(0, current - 1));
    }
    return updated;
  }, [notifications]);

  const markAllAsRead = useCallback(async () => {
    await notificationService.markAllAsRead();
    const readAt = new Date().toISOString();
    setNotifications((current) =>
      current.map((item) =>
        item.isRead ? item : { ...item, isRead: true, readAt },
      ),
    );
    setUnreadCount(0);
  }, []);

  return {
    notifications,
    unreadCount,
    isLoading,
    error,
    toastNotification,
    dismissToast: () => setToastNotification(null),
    markAsRead,
    markAllAsRead,
    refresh: load,
  };
}
