"use client";

import {
  CheckCircle2,
  Clock3,
  Files,
  Radio,
  WifiOff,
} from "lucide-react";
import { useCallback, useMemo } from "react";

import { DocumentDropzone } from "@/components/document/document-dropzone";
import { DocumentList } from "@/components/document/document-list";
import { useDocumentProgress } from "@/hooks/use-document-progress";
import { useDocuments } from "@/hooks/use-documents";
import { useWorkspace } from "@/hooks/use-workspace";
import { cn } from "@/lib/utils";
import type { Workspace } from "@/types/workspace.types";

export function DocumentManager({ workspace }: { workspace: Workspace }) {
  const {
    documents,
    isLoading,
    error,
    uploadDocument,
    applyProgressUpdates,
    reload,
  } = useDocuments(workspace.id);
  const { incrementDocumentCount } = useWorkspace();
  const realtimeStatus = useDocumentProgress(
    workspace.id,
    applyProgressUpdates,
  );

  const handleUpload = useCallback(
    async (file: File) => {
      const document = await uploadDocument(file);
      incrementDocumentCount(workspace.id);
      return document;
    },
    [incrementDocumentCount, uploadDocument, workspace.id],
  );

  const stats = useMemo(() => {
    const indexed = documents.filter(
      (document) => document.status === "Indexed",
    ).length;
    const processing = documents.filter(
      (document) =>
        document.status === "Processing" || document.status === "Pending",
    ).length;

    return {
      total: documents.length,
      indexed,
      processing,
    };
  }, [documents]);

  const realtimeConnected = realtimeStatus === "connected";

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight text-content">
              {workspace.name}
            </h1>
            <span
              className={cn(
                "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset",
                realtimeConnected
                  ? "bg-success-subtle text-success ring-success"
                  : realtimeStatus === "connecting" ||
                      realtimeStatus === "reconnecting"
                    ? "bg-warning-subtle text-warning ring-warning"
                    : "bg-surface-tertiary text-content-secondary ring-line",
              )}
            >
              {realtimeConnected ? (
                <Radio className="size-3 animate-pulse" />
              ) : (
                <WifiOff className="size-3" />
              )}
              {realtimeConnected
                ? "Realtime"
                : realtimeStatus === "connecting" ||
                    realtimeStatus === "reconnecting"
                  ? "Đang kết nối"
                  : "Ngoại tuyến"}
            </span>
          </div>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted">
            {workspace.description ||
              "Quản lý và theo dõi quá trình lập chỉ mục tài liệu PDF trong workspace."}
          </p>
        </div>
        <p className="text-xs text-muted">
          Quyền truy cập:{" "}
          <span
            className={cn(
              "rounded-full px-2 py-1 font-semibold ring-1 ring-inset",
              workspace.role === "Owner"
                ? "bg-role-owner-subtle text-role-owner ring-role-owner-line"
                : workspace.role === "Admin"
                  ? "bg-role-admin-subtle text-role-admin ring-role-admin-line"
                  : "bg-role-member-subtle text-role-member ring-role-member-line",
            )}
          >
            {workspace.role}
          </span>
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard
          icon={Files}
          label="Tổng tài liệu"
          tone="info"
          value={stats.total}
        />
        <StatCard
          icon={Clock3}
          label="Đang xử lý"
          tone="warning"
          value={stats.processing}
        />
        <StatCard
          icon={CheckCircle2}
          label="Đã lập chỉ mục"
          tone="success"
          value={stats.indexed}
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(18rem,0.75fr)_minmax(0,1.75fr)] xl:items-start">
        <DocumentDropzone onUpload={handleUpload} />
        <DocumentList
          documents={documents}
          error={error}
          isLoading={isLoading}
          onRetry={() => void reload()}
        />
      </div>
    </div>
  );
}

function StatCard({
  icon: Icon,
  label,
  value,
  tone,
}: {
  icon: typeof Files;
  label: string;
  value: number;
  tone: "info" | "warning" | "success";
}) {
  const toneClasses = {
    info: "bg-info-subtle text-info",
    warning: "bg-warning-subtle text-warning",
    success: "bg-success-subtle text-success",
  };

  return (
    <div className="glass-panel flex items-center gap-3 rounded-2xl p-4">
      <span
        className={cn(
          "flex size-10 items-center justify-center rounded-xl",
          toneClasses[tone],
        )}
      >
        <Icon className="size-5" />
      </span>
      <div>
        <p className="text-2xl font-semibold tabular-nums text-content">
          {value}
        </p>
        <p className="text-xs text-muted">{label}</p>
      </div>
    </div>
  );
}
