import { FileText } from "lucide-react";

import { cn } from "@/lib/utils";
import type { Citation } from "@/types/chat.types";

export function getCitationKey(citation: Citation) {
  return `${citation.chunkId}:${citation.documentId}:${citation.pageNumber}`;
}

export function CitationBadge({
  citation,
  index,
  active,
  onSelect,
}: {
  citation: Citation;
  index: number;
  active: boolean;
  onSelect: (citation: Citation, index: number) => void;
}) {
  return (
    <button
      aria-label={`Trích dẫn trang ${citation.pageNumber}, ${citation.documentName}`}
      aria-pressed={active}
      className={cn(
        "inline-flex min-h-9 items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs font-semibold transition-[background-color,border-color,color,box-shadow] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface",
        active
          ? "border-citation-active-line bg-citation-active-subtle text-citation-active shadow-[0_0_18px_var(--citation-active-glow)]"
          : "border-citation-line bg-citation-subtle text-citation hover:bg-citation-hover",
      )}
      onClick={() => onSelect(citation, index)}
      title={`${citation.documentName}, trang ${citation.pageNumber}`}
      type="button"
    >
      <FileText className="size-3.5" />
      [{index}]
    </button>
  );
}
