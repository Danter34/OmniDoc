import { FileText } from "lucide-react";

import type { Citation } from "@/types/chat.types";

export function CitationBadge({
  citation,
  index,
  onSelect,
}: {
  citation: Citation;
  index: number;
  onSelect: (citation: Citation, index: number) => void;
}) {
  return (
    <button
      aria-label={`Mở nguồn ${index}`}
      className="inline-flex items-center gap-1.5 rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-1.5 text-xs font-medium text-amber-700 transition hover:border-amber-300 hover:bg-amber-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-2"
      onClick={() => onSelect(citation, index)}
      title={`${citation.documentName}, trang ${citation.pageNumber}`}
      type="button"
    >
      <FileText className="size-3.5" />
      [{index}]
    </button>
  );
}
