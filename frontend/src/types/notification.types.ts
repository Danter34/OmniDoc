export type NotificationType =
  | "WorkspaceInvitation"
  | "DocumentProcessed"
  | "System";

export interface AppNotification {
  id: string;
  title: string;
  message: string;
  actionUrl: string | null;
  type: NotificationType;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
  metadataJson: string | null;
}

export interface NotificationPage {
  items: AppNotification[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface NotificationCount {
  count: number;
}
