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
    badgeClass: "bg-slate-100 text-slate-700 ring-slate-200",
    icon: CircleDashed,
  },
  Processing: {
    label: "Đang xử lý",
    badgeClass: "bg-blue-50 text-blue-700 ring-blue-200",
    icon: LoaderCircle,
    spinning: true,
  },
  Extracting: {
    label: "Trích xuất",
    badgeClass: "bg-blue-50 text-blue-700 ring-blue-200",
    icon: FileSearch,
  },
  Chunking: {
    label: "Chia đoạn",
    badgeClass: "bg-violet-50 text-violet-700 ring-violet-200",
    icon: Scissors,
  },
  Embedding: {
    label: "Embedding",
    badgeClass: "bg-indigo-50 text-indigo-700 ring-indigo-200",
    icon: Sparkles,
  },
  Completed: {
    label: "Đã lập chỉ mục",
    badgeClass: "bg-emerald-50 text-emerald-700 ring-emerald-200",
    icon: CheckCircle2,
  },
  Indexed: {
    label: "Đã lập chỉ mục",
    badgeClass: "bg-emerald-50 text-emerald-700 ring-emerald-200",
    icon: CheckCircle2,
  },
  Failed: {
    label: "Thất bại",
    badgeClass: "bg-rose-50 text-rose-700 ring-rose-200",
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
