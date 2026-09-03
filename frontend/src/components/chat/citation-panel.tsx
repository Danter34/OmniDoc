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
      className="fixed inset-0 z-50 bg-slate-950/35 backdrop-blur-[1px] lg:bg-slate-950/15"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
      role="presentation"
    >
      <aside
        aria-label={`Chi tiết nguồn ${index}`}
        aria-modal="true"
        className="absolute inset-x-0 bottom-0 max-h-[85vh] overflow-y-auto rounded-t-3xl border border-slate-200 bg-white shadow-2xl lg:inset-y-4 lg:left-auto lg:right-4 lg:w-[27rem] lg:rounded-3xl"
        role="dialog"
      >
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-slate-100 bg-white/95 px-5 py-4 backdrop-blur">
          <div className="flex items-center gap-2.5">
            <span className="flex size-9 items-center justify-center rounded-xl bg-amber-100 text-amber-700">
              <BookOpenText className="size-4.5" />
            </span>
            <div>
              <p className="text-xs font-medium uppercase tracking-wider text-amber-600">
                Nguồn [{index}]
              </p>
              <h2 className="text-sm font-semibold text-slate-900">
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
          <section className="rounded-2xl border border-slate-200 bg-slate-50/70 p-4">
            <div className="flex items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-white text-blue-600 shadow-sm ring-1 ring-slate-200">
                <FileText className="size-5" />
              </span>
              <div className="min-w-0">
                <p className="break-words text-sm font-semibold text-slate-900">
                  {citation.documentName}
                </p>
                <p className="mt-1 text-xs text-slate-500">
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
              <Quote className="size-4 text-amber-600" />
              <h3 className="text-sm font-semibold text-slate-800">
                Trích đoạn gốc
              </h3>
            </div>
            <blockquote className="rounded-2xl border border-amber-200 bg-amber-50/60 p-4 text-sm leading-7 text-slate-700">
              <mark className="bg-amber-100/80 text-inherit">
                {citation.snippet}
              </mark>
            </blockquote>
          </section>

          <section className="rounded-2xl border border-slate-200 p-4">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <Sparkles className="size-4 text-violet-600" />
                <span className="text-sm font-medium text-slate-700">
                  Độ tương đồng ngữ nghĩa
                </span>
              </div>
              <span className="rounded-full bg-violet-50 px-2.5 py-1 text-xs font-semibold text-violet-700 ring-1 ring-inset ring-violet-200">
                {score}%
              </span>
            </div>
            <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-100">
              <div
                className="h-full rounded-full bg-gradient-to-r from-violet-500 to-blue-500"
                style={{ width: `${score}%` }}
              />
            </div>
            <p className="mt-2 text-xs leading-5 text-slate-500">
              Điểm thể hiện mức độ liên quan giữa câu hỏi và đoạn văn được truy
              xuất.
            </p>
          </section>
        </div>
      </aside>
    </div>
  );
}
