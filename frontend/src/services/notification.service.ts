import { apiRequest } from "@/services/api-client";
import type {
  AppNotification,
  NotificationCount,
  NotificationPage,
} from "@/types/notification.types";

export const notificationService = {
  getAll(page = 1, pageSize = 20) {
    return apiRequest<NotificationPage>(
      `/api/notifications?page=${page}&pageSize=${pageSize}`,
    );
  },

  getUnreadCount() {
    return apiRequest<NotificationCount>("/api/notifications/unread-count");
  },

  markAsRead(id: string) {
    return apiRequest<AppNotification>(`/api/notifications/${id}/read`, {
      method: "PATCH",
    });
  },

  markAllAsRead() {
    return apiRequest<NotificationCount>("/api/notifications/read-all", {
      method: "PATCH",
    });
  },
};
