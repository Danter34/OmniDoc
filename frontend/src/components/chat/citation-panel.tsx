"use client";

import {
  BookOpenText,
  ExternalLink,
  FileText,
  Quote,
  Sparkles,
  X,
} from "lucide-react";
import { useEffect } from "react";

import { Button } from "@/components/ui/button";
import type { Citation } from "@/types/chat.types";

export interface SelectedCitation {
  citation: Citation;
  index: number;
}

export function CitationPanel({
  selected,
  onClose,
  onViewInDocument,
}: {
  selected: SelectedCitation | null;
  onClose: () => void;
  onViewInDocument: (citation: Citation) => void;
}) {
  useEffect(() => {
    if (!selected) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose, selected]);

  if (!selected) {
    return null;
  }

  const { citation, index } = selected;
  const rawScore =
    citation.similarityScore <= 1
      ? citation.similarityScore * 100
      : citation.similarityScore;
  const score = Math.min(100, Math.max(0, Math.round(rawScore)));

  return (
    <div
      className="fixed inset-0 z-50 bg-overlay backdrop-blur-[1px]"
      onPointerDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
      role="presentation"
    >
      <aside
        aria-label={`Chi tiết nguồn ${index}`}
        aria-modal="true"
        className="glass-panel absolute inset-x-0 bottom-0 max-h-[85vh] overflow-y-auto rounded-t-3xl lg:inset-y-4 lg:left-auto lg:right-4 lg:w-[27rem] lg:rounded-3xl"
        role="dialog"
      >
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-line-subtle bg-elevated/95 px-5 py-4 backdrop-blur">
          <div className="flex items-center gap-2.5">
            <span className="flex size-9 items-center justify-center rounded-xl bg-citation-subtle text-citation">
              <BookOpenText className="size-4.5" />
            </span>
            <div>
              <p className="text-xs font-medium uppercase tracking-wider text-citation">
                Nguồn [{index}]
              </p>
              <h2 className="text-sm font-semibold text-content">
                Chi tiết trích dẫn
              </h2>
            </div>
          </div>
          <Button
            aria-label="Đóng nguồn"
            className="size-9 px-0"
            onClick={onClose}
            variant="ghost"
          >
            <X className="size-5" />
          </Button>
        </div>

        <div className="space-y-5 p-5">
          <section className="rounded-2xl border border-line-subtle bg-surface-subtle/70 p-4">
            <div className="flex items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-surface text-accent shadow-sm ring-1 ring-line-subtle">
                <FileText className="size-5" />
              </span>
              <div className="min-w-0">
                <p className="break-words text-sm font-semibold text-content">
                  {citation.documentName}
                </p>
                <p className="mt-1 text-xs text-muted">
                  Tài liệu PDF · Trang {citation.pageNumber}
                </p>
              </div>
            </div>
            <Button
              className="mt-4 w-full"
              icon={<ExternalLink className="size-4" />}
              onClick={() => onViewInDocument(citation)}
              size="sm"
              variant="secondary"
            >
              Xem vị trí trong tài liệu
            </Button>
          </section>

          <section>
            <div className="mb-2.5 flex items-center gap-2">
              <Quote className="size-4 text-citation" />
              <h3 className="text-sm font-semibold text-content">
                Trích đoạn gốc
              </h3>
            </div>
            <blockquote className="rounded-2xl border border-citation-line bg-citation-subtle p-4 text-sm leading-7 text-content-secondary">
              <mark className="bg-citation-hover text-inherit">
                {citation.snippet}
              </mark>
            </blockquote>
          </section>

          <section className="rounded-2xl border border-line-subtle bg-surface/60 p-4">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <Sparkles className="size-4 text-citation-active" />
                <span className="text-sm font-medium text-content-secondary">
                  Độ tương đồng ngữ nghĩa
                </span>
              </div>
              <span className="rounded-full bg-citation-active-subtle px-2.5 py-1 text-xs font-semibold text-citation-active ring-1 ring-inset ring-citation-active-line">
                {score}%
              </span>
            </div>
            <div className="mt-3 h-2 overflow-hidden rounded-full bg-surface-tertiary">
              <div
                className="h-full rounded-full [background-image:var(--gradient-brand)]"
                style={{ width: `${score}%` }}
              />
            </div>
            <p className="mt-2 text-xs leading-5 text-muted">
              Điểm thể hiện mức độ liên quan giữa câu hỏi và đoạn văn được truy
              xuất.
            </p>
          </section>
        </div>
      </aside>
    </div>
  );
}
