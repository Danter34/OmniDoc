import { FileText, TriangleAlert } from "lucide-react";
import { memo } from "react";

import { DocumentStatusBadge } from "@/components/document/document-status-badge";
import { cn, formatDateTime, formatFileSize } from "@/lib/utils";
import type { WorkspaceDocument } from "@/types/document.types";

function DocumentItemComponent({
  document,
}: {
  document: WorkspaceDocument;
}) {
  const processing =
    document.status === "Processing" &&
    document.stage !== "Indexed" &&
    document.stage !== "Completed";

  return (
    <article className="grid gap-4 border-t border-line-subtle px-4 py-4 transition-colors hover:bg-surface-subtle/70 sm:px-5 lg:grid-cols-[minmax(0,1fr)_9rem_12rem_12rem] lg:items-center">
      <div className="flex min-w-0 items-start gap-3">
        <span
          className={cn(
            "flex size-10 shrink-0 items-center justify-center rounded-xl",
            document.status === "Failed"
              ? "bg-danger-subtle text-danger"
              : "bg-info-subtle text-accent",
          )}
        >
          <FileText className="size-5" />
        </span>
        <div className="min-w-0">
          <p
            className="truncate text-sm font-medium text-content"
            title={document.fileName}
          >
            {document.title || document.fileName}
          </p>
          <p className="mt-1 truncate text-xs text-muted">
            {document.fileName}
          </p>
          {document.status === "Failed" && document.errorMessage ? (
            <p
              className="mt-2 flex max-w-xl items-start gap-1.5 text-xs leading-5 text-danger"
              title={document.errorMessage}
            >
              <TriangleAlert className="mt-0.5 size-3.5 shrink-0" />
              <span className="line-clamp-2">{document.errorMessage}</span>
            </p>
          ) : null}
        </div>
      </div>

      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-muted lg:hidden">
          Kích thước
        </p>
        <p className="mt-1 text-sm text-content-secondary lg:mt-0">
          {formatFileSize(document.fileSizeBytes)}
        </p>
      </div>

      <div>
        <p className="text-[11px] font-medium uppercase tracking-wide text-muted lg:hidden">
          Tải lên lúc
        </p>
        <p className="mt-1 text-sm text-content-secondary lg:mt-0">
          {formatDateTime(document.createdAtUtc)}
        </p>
      </div>

      <div className="min-w-0">
        <p className="mb-1 text-[11px] font-medium uppercase tracking-wide text-muted lg:hidden">
          Trạng thái
        </p>
        <DocumentStatusBadge document={document} />
        {processing ? (
          <div className="mt-2.5">
            <div className="mb-1 flex items-center justify-between text-[11px] text-muted">
              <span>{document.stage}</span>
              <span className="font-medium tabular-nums text-content-secondary">
                {document.progress}%
              </span>
            </div>
            <div
              aria-label={`Tiến độ ${document.progress}%`}
              aria-valuemax={100}
              aria-valuemin={0}
              aria-valuenow={document.progress}
              className="h-1.5 overflow-hidden rounded-full bg-surface-tertiary"
              role="progressbar"
            >
              <div
                className="progress-shimmer relative h-full origin-left rounded-full transition-transform duration-500 ease-out [background-image:var(--gradient-progress)]"
                style={{ transform: `scaleX(${document.progress / 100})` }}
              />
            </div>
          </div>
        ) : null}
      </div>
    </article>
  );
}

export const DocumentItem = memo(DocumentItemComponent);
