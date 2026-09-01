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
            <h1 className="text-2xl font-semibold tracking-tight text-slate-950">
              {workspace.name}
            </h1>
            <span
              className={cn(
                "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset",
                realtimeConnected
                  ? "bg-emerald-50 text-emerald-700 ring-emerald-200"
                  : realtimeStatus === "connecting" ||
                      realtimeStatus === "reconnecting"
                    ? "bg-amber-50 text-amber-700 ring-amber-200"
                    : "bg-slate-100 text-slate-600 ring-slate-200",
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
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">
            {workspace.description ||
              "Quản lý và theo dõi quá trình lập chỉ mục tài liệu PDF trong workspace."}
          </p>
        </div>
        <p className="text-xs text-slate-400">
          Quyền truy cập:{" "}
          <span className="font-medium text-slate-600">
            {workspace.role === "Member" ? "Member" : "Owner"}
          </span>
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard
          icon={Files}
          label="Tổng tài liệu"
          tone="blue"
          value={stats.total}
        />
        <StatCard
          icon={Clock3}
          label="Đang xử lý"
          tone="amber"
          value={stats.processing}
        />
        <StatCard
          icon={CheckCircle2}
          label="Đã lập chỉ mục"
          tone="emerald"
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
  tone: "blue" | "amber" | "emerald";
}) {
  const toneClasses = {
    blue: "bg-blue-50 text-blue-600",
    amber: "bg-amber-50 text-amber-600",
    emerald: "bg-emerald-50 text-emerald-600",
  };

  return (
    <div className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <span
        className={cn(
          "flex size-10 items-center justify-center rounded-xl",
          toneClasses[tone],
        )}
      >
        <Icon className="size-5" />
      </span>
      <div>
        <p className="text-2xl font-semibold tabular-nums text-slate-950">
          {value}
        </p>
        <p className="text-xs text-slate-500">{label}</p>
      </div>
    </div>
  );
}
