import {
  CheckCircle2,
  CircleDashed,
  FileSearch,
  LoaderCircle,
  Scissors,
  Sparkles,
  XCircle,
} from "lucide-react";

import { cn } from "@/lib/utils";
import type { WorkspaceDocument } from "@/types/document.types";

interface StatusConfig {
  label: string;
  badgeClass: string;
  icon: typeof CircleDashed;
  spinning?: boolean;
}

const statusConfig: Record<string, StatusConfig> = {
  Pending: {
    label: "Đang chờ",
    badgeClass: "bg-surface-tertiary text-content-secondary ring-line",
    icon: CircleDashed,
  },
  Processing: {
    label: "Đang xử lý",
    badgeClass: "bg-info-subtle text-info ring-info",
    icon: LoaderCircle,
    spinning: true,
  },
  Extracting: {
    label: "Trích xuất",
    badgeClass: "bg-info-subtle text-info ring-info",
    icon: FileSearch,
  },
  Chunking: {
    label: "Chia đoạn",
    badgeClass: "bg-role-admin-subtle text-role-admin ring-role-admin-line",
    icon: Scissors,
  },
  Embedding: {
    label: "Embedding",
    badgeClass: "bg-role-owner-subtle text-role-owner ring-role-owner-line",
    icon: Sparkles,
  },
  Completed: {
    label: "Đã lập chỉ mục",
    badgeClass: "bg-success-subtle text-success ring-success",
    icon: CheckCircle2,
  },
  Indexed: {
    label: "Đã lập chỉ mục",
    badgeClass: "bg-success-subtle text-success ring-success",
    icon: CheckCircle2,
  },
  Failed: {
    label: "Thất bại",
    badgeClass: "bg-danger-subtle text-danger ring-danger",
    icon: XCircle,
  },
};

export function DocumentStatusBadge({
  document,
}: {
  document: WorkspaceDocument;
}) {
  const config = statusConfig[document.stage] ?? statusConfig[document.status];
  const Icon = config.icon;

  return (
    <span
      className={cn(
        "inline-flex w-fit items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset",
        config.badgeClass,
      )}
      title={
        document.stage === "Failed"
          ? document.errorMessage ?? "Không thể xử lý tài liệu."
          : undefined
      }
    >
      <Icon className={cn("size-3.5", config.spinning && "animate-spin")} />
      {config.label}
    </span>
  );
}
