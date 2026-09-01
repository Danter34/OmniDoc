import { FileQuestion, RefreshCw } from "lucide-react";

import { DocumentItem } from "@/components/document/document-item";
import { Button } from "@/components/ui/button";
import type { WorkspaceDocument } from "@/types/document.types";

interface DocumentListProps {
  documents: WorkspaceDocument[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

function DocumentListSkeleton() {
  return (
    <div className="divide-y divide-slate-100">
      {[0, 1, 2].map((item) => (
        <div
          className="grid animate-pulse gap-4 px-5 py-5 lg:grid-cols-[minmax(0,1fr)_9rem_12rem_12rem]"
          key={item}
        >
          <div className="flex items-center gap-3">
            <div className="size-10 rounded-xl bg-slate-100" />
            <div className="flex-1">
              <div className="h-3.5 w-2/3 rounded bg-slate-100" />
              <div className="mt-2 h-3 w-1/3 rounded bg-slate-100" />
            </div>
          </div>
          <div className="h-4 w-16 rounded bg-slate-100" />
          <div className="h-4 w-28 rounded bg-slate-100" />
          <div className="h-6 w-24 rounded-full bg-slate-100" />
        </div>
      ))}
    </div>
  );
}

export function DocumentList({
  documents,
  isLoading,
  error,
  onRetry,
}: DocumentListProps) {
  return (
    <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
      <div className="flex items-center justify-between border-b border-slate-100 px-4 py-4 sm:px-5">
        <div>
          <h2 className="font-semibold text-slate-900">Tài liệu</h2>
          <p className="mt-0.5 text-xs text-slate-500">
            {documents.length} tài liệu trong workspace
          </p>
        </div>
      </div>

      <div className="hidden grid-cols-[minmax(0,1fr)_9rem_12rem_12rem] bg-slate-50/80 px-5 py-2.5 text-[11px] font-semibold uppercase tracking-wider text-slate-400 lg:grid">
        <span>Tên tài liệu</span>
        <span>Kích thước</span>
        <span>Thời gian tải lên</span>
        <span>Trạng thái</span>
      </div>

      {isLoading ? <DocumentListSkeleton /> : null}

      {!isLoading && error ? (
        <div className="px-6 py-12 text-center">
          <p className="text-sm font-medium text-slate-800">
            Không thể tải danh sách tài liệu
          </p>
          <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-slate-500">
            {error}
          </p>
          <Button
            className="mt-5"
            icon={<RefreshCw className="size-4" />}
            onClick={onRetry}
            variant="secondary"
          >
            Thử lại
          </Button>
        </div>
      ) : null}

      {!isLoading && !error && documents.length === 0 ? (
        <div className="px-6 py-14 text-center">
          <span className="mx-auto flex size-12 items-center justify-center rounded-2xl bg-slate-100 text-slate-400">
            <FileQuestion className="size-6" />
          </span>
          <p className="mt-4 text-sm font-medium text-slate-800">
            Chưa có tài liệu
          </p>
          <p className="mt-1.5 text-sm text-slate-500">
            Tải PDF đầu tiên để bắt đầu xây dựng kho tri thức.
          </p>
        </div>
      ) : null}

      {!isLoading && !error
        ? documents.map((document) => (
            <DocumentItem document={document} key={document.id} />
          ))
        : null}
    </section>
  );
}
