"use client";

import {
  ChevronLeft,
  ChevronRight,
  Download,
  FileWarning,
  Maximize2,
  Minus,
  Plus,
  RefreshCw,
  X,
} from "lucide-react";
import { memo, useEffect, useMemo, useState } from "react";

import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import { documentService } from "@/services/document.service";
import type { WorkspaceDocument } from "@/types/document.types";

const MIN_ZOOM = 50;
const MAX_ZOOM = 200;
const ZOOM_STEP = 25;

export interface PdfPageTarget {
  pageNumber: number;
  requestId: number;
  fromCitation: boolean;
}

function readPageCount(buffer: ArrayBuffer) {
  const source = new TextDecoder("windows-1252").decode(buffer);
  const treeCounts = Array.from(
    source.matchAll(/\/Type\s*\/Pages\b[\s\S]{0,300}?\/Count\s+(\d+)/g),
    (match) => Number(match[1]),
  ).filter(Number.isFinite);

  if (treeCounts.length > 0) {
    return Math.max(...treeCounts);
  }

  const pageObjects = source.match(/\/Type\s*\/Page\b/g)?.length ?? 0;
  return pageObjects || null;
}

function clampPage(value: number, pageCount: number | null) {
  const upperBound = pageCount ?? 99_999;
  return Math.min(upperBound, Math.max(1, Math.trunc(value) || 1));
}

function PdfViewerComponent({
  workspaceId,
  document,
  documents,
  target,
  onClose,
  onDocumentSelect,
}: {
  workspaceId: string;
  document: WorkspaceDocument;
  documents: WorkspaceDocument[];
  target: PdfPageTarget | null;
  onClose: () => void;
  onDocumentSelect: (documentId: string) => void;
}) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const [navigation, setNavigation] = useState({
    pageNumber: 1,
    targetRequestId: -1,
  });
  const [pageCount, setPageCount] = useState<number | null>(null);
  const [zoom, setZoom] = useState<number | "page-width">("page-width");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    let nextObjectUrl: string | null = null;

    documentService
      .getContent(workspaceId, document.id, controller.signal)
      .then(async (blob) => {
        const buffer = await blob.arrayBuffer();
        const signature = new TextDecoder().decode(buffer.slice(0, 5));

        if (signature !== "%PDF-") {
          throw new Error("Tệp nhận được không phải là một tài liệu PDF hợp lệ.");
        }

        if (controller.signal.aborted) {
          return;
        }

        const pdfBlob = new Blob([buffer], { type: "application/pdf" });
        nextObjectUrl = URL.createObjectURL(pdfBlob);
        setPageCount(readPageCount(buffer));
        setObjectUrl(nextObjectUrl);
      })
      .catch((requestError: unknown) => {
        if (
          requestError instanceof DOMException &&
          requestError.name === "AbortError"
        ) {
          return;
        }

        setError(getErrorMessage(requestError));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => {
      controller.abort();
      if (nextObjectUrl) {
        URL.revokeObjectURL(nextObjectUrl);
      }
    };
  }, [document.id, retryKey, workspaceId]);

  const hasPendingTarget =
    target !== null && target.requestId !== navigation.targetRequestId;
  const pageNumber = clampPage(
    hasPendingTarget ? target.pageNumber : navigation.pageNumber,
    pageCount,
  );

  const viewerUrl = useMemo(() => {
    if (!objectUrl) {
      return null;
    }

    const zoomValue = zoom === "page-width" ? "page-width" : zoom;
    return `${objectUrl}#page=${pageNumber}&zoom=${zoomValue}&toolbar=1&navpanes=0`;
  }, [objectUrl, pageNumber, zoom]);

  function changePage(value: number) {
    setNavigation({
      pageNumber: clampPage(value, pageCount),
      targetRequestId: target?.requestId ?? -1,
    });
  }

  function changeZoom(value: number | "page-width") {
    setZoom(value);
  }

  function downloadPdf() {
    if (!objectUrl) {
      return;
    }

    const anchor = window.document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = document.fileName;
    anchor.click();
  }

  const numericZoom = zoom === "page-width" ? 100 : zoom;

  return (
    <div className="flex h-full min-h-0 flex-col bg-surface-tertiary">
      <div className="glass-panel relative z-20 flex min-h-16 shrink-0 flex-wrap items-center gap-2 rounded-none border-x-0 border-t-0 px-3 py-2">
        <div className="mr-auto min-w-0 basis-[12rem]">
          <label className="sr-only" htmlFor="pdf-viewer-document-selector">
            Tài liệu đang xem
          </label>
          <select
            className="block h-9 w-full truncate rounded-md border-0 bg-transparent px-2 text-sm font-semibold text-content outline-none hover:bg-surface-subtle focus:ring-2 focus:ring-focus-glow"
            id="pdf-viewer-document-selector"
            onChange={(event) => onDocumentSelect(event.target.value)}
            title={document.fileName}
            value={document.id}
          >
            {documents.map((item) => (
              <option key={item.id} value={item.id}>
                {item.title || item.fileName}
              </option>
            ))}
          </select>
          <p className="mt-0.5 truncate text-[11px] text-muted">
            {document.fileName}
          </p>
        </div>

        <div className="flex items-center rounded-lg border border-line-subtle bg-surface-subtle p-0.5">
          <button
            aria-label="Trang trước"
            className="flex size-9 items-center justify-center rounded-md text-content-secondary transition-colors hover:bg-surface disabled:opacity-40"
            disabled={pageNumber <= 1 || isLoading || Boolean(error)}
            onClick={() => changePage(pageNumber - 1)}
            type="button"
          >
            <ChevronLeft className="size-4" />
          </button>
          <label className="flex items-center gap-1 px-1 text-xs text-muted">
            <span className="sr-only">Trang hiện tại</span>
            <input
              aria-label="Trang hiện tại"
              className="h-9 w-12 rounded border border-line-subtle bg-surface px-1 text-center font-medium tabular-nums text-content outline-none focus:border-focus-ring focus:ring-2 focus:ring-focus-glow"
              disabled={isLoading || Boolean(error)}
              max={pageCount ?? undefined}
              min={1}
              onChange={(event) => changePage(Number(event.target.value))}
              type="number"
              value={pageNumber}
            />
            <span className="whitespace-nowrap tabular-nums">
              / {pageCount ?? "—"}
            </span>
          </label>
          <button
            aria-label="Trang tiếp theo"
            className="flex size-9 items-center justify-center rounded-md text-content-secondary transition-colors hover:bg-surface disabled:opacity-40"
            disabled={
              isLoading ||
              Boolean(error) ||
              (pageCount !== null && pageNumber >= pageCount)
            }
            onClick={() => changePage(pageNumber + 1)}
            type="button"
          >
            <ChevronRight className="size-4" />
          </button>
        </div>

        <div className="flex items-center rounded-lg border border-line-subtle bg-surface-subtle p-0.5">
          <button
            aria-label="Thu nhỏ"
            className="flex size-9 items-center justify-center rounded-md text-content-secondary transition-colors hover:bg-surface disabled:opacity-40"
            disabled={isLoading || numericZoom <= MIN_ZOOM || Boolean(error)}
            onClick={() => changeZoom(Math.max(MIN_ZOOM, numericZoom - ZOOM_STEP))}
            type="button"
          >
            <Minus className="size-3.5" />
          </button>
          <span className="w-11 text-center text-xs font-medium tabular-nums text-content-secondary">
            {zoom === "page-width" ? "Fit" : `${zoom}%`}
          </span>
          <button
            aria-label="Phóng to"
            className="flex size-9 items-center justify-center rounded-md text-content-secondary transition-colors hover:bg-surface disabled:opacity-40"
            disabled={isLoading || numericZoom >= MAX_ZOOM || Boolean(error)}
            onClick={() => changeZoom(Math.min(MAX_ZOOM, numericZoom + ZOOM_STEP))}
            type="button"
          >
            <Plus className="size-3.5" />
          </button>
          <button
            aria-label="Vừa chiều rộng"
            className={cn(
              "flex size-9 items-center justify-center rounded-md text-content-secondary transition-colors hover:bg-surface disabled:opacity-40",
              zoom === "page-width" && "bg-surface text-accent shadow-sm",
            )}
            disabled={isLoading || Boolean(error)}
            onClick={() => changeZoom("page-width")}
            title="Vừa chiều rộng"
            type="button"
          >
            <Maximize2 className="size-3.5" />
          </button>
        </div>

        <Button
          aria-label="Tải tài liệu"
          className="size-9 px-0"
          disabled={!objectUrl || Boolean(error)}
          onClick={downloadPdf}
          title="Tải tài liệu"
          variant="ghost"
        >
          <Download className="size-4" />
        </Button>
        <Button
          aria-label="Đóng trình xem PDF"
          className="size-9 px-0"
          onClick={onClose}
          title="Đóng trình xem PDF"
          variant="ghost"
        >
          <X className="size-4.5" />
        </Button>
      </div>

      <div className="relative min-h-0 flex-1 overflow-hidden bg-surface-tertiary">
        {isLoading ? <PdfViewerSkeleton /> : null}

        {!isLoading && error ? (
          <div className="flex h-full items-center justify-center p-6 text-center">
            <div className="max-w-sm rounded-2xl border border-danger bg-surface p-6 shadow-sm">
              <span className="mx-auto flex size-11 items-center justify-center rounded-xl bg-danger-subtle text-danger">
                <FileWarning className="size-5" />
              </span>
              <p className="mt-4 text-sm font-semibold text-content">
                Không thể mở tài liệu PDF
              </p>
              <p className="mt-2 text-sm leading-6 text-muted">{error}</p>
              <Button
                className="mt-5"
                icon={<RefreshCw className="size-4" />}
                onClick={() => {
                  setError(null);
                  setObjectUrl(null);
                  setPageCount(null);
                  setIsLoading(true);
                  setRetryKey((value) => value + 1);
                }}
                size="sm"
                variant="secondary"
              >
                Thử lại
              </Button>
            </div>
          </div>
        ) : null}

        {!isLoading && !error && viewerUrl ? (
          <PdfFrame
            fileName={document.fileName}
            key={viewerUrl}
            pageNumber={pageNumber}
            viewerUrl={viewerUrl}
          />
        ) : null}

        {target?.fromCitation && !error ? (
          <div
            aria-hidden="true"
            className="pdf-page-citation-aura"
            key={`aura-${target.requestId}`}
          />
        ) : null}

        {target?.fromCitation && !error ? (
          <div
            aria-live="polite"
            className="pdf-citation-notice pointer-events-none absolute bottom-4 left-1/2 z-20 -translate-x-1/2 whitespace-nowrap rounded-full border border-citation-active-line bg-citation-active-subtle px-3.5 py-2 text-xs font-semibold text-citation-active shadow-[0_0_22px_var(--citation-active-glow)]"
            key={target.requestId}
          >
            Đang xem trang trích dẫn {target.pageNumber}
          </div>
        ) : null}
      </div>
    </div>
  );
}

function PdfViewerSkeleton() {
  return (
    <div className="flex h-full flex-col items-center gap-4 overflow-hidden px-6 py-8">
      <div className="flex items-center gap-2 text-sm text-muted">
        <Spinner className="size-5 text-accent" />
        Đang tải tài liệu an toàn...
      </div>
      <div className="h-full w-full max-w-2xl animate-pulse rounded-md bg-pdf-page shadow-sm">
        <div className="space-y-3 p-10">
          <div className="h-5 w-2/3 rounded bg-pdf-placeholder" />
          <div className="h-3 w-full rounded bg-pdf-placeholder" />
          <div className="h-3 w-5/6 rounded bg-pdf-placeholder" />
          <div className="h-3 w-11/12 rounded bg-pdf-placeholder" />
        </div>
      </div>
    </div>
  );
}

function PdfFrame({
  viewerUrl,
  fileName,
  pageNumber,
}: {
  viewerUrl: string;
  fileName: string;
  pageNumber: number;
}) {
  const [isLoading, setIsLoading] = useState(true);

  return (
    <>
      {isLoading ? (
        <div className="absolute inset-0 z-10 flex items-center justify-center bg-surface-tertiary/90">
          <div className="flex items-center gap-2 text-sm text-muted">
            <Spinner className="size-5 text-accent" />
            Đang hiển thị trang {pageNumber}...
          </div>
        </div>
      ) : null}
      <iframe
        className="h-full w-full border-0 bg-pdf-page"
        onLoad={() => setIsLoading(false)}
        src={viewerUrl}
        title={`Tài liệu PDF ${fileName}, trang ${pageNumber}`}
      />
    </>
  );
}

export const PdfViewer = memo(PdfViewerComponent);
