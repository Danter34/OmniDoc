"use client";

import { FileText, MessageSquareText } from "lucide-react";
import { useRef, useState } from "react";

const pages = [
  { page: 2, title: "01 / Quy trình phê duyệt", quote: "Mọi đề nghị mua sắm cần được trưởng bộ phận phê duyệt trước khi chuyển đến phòng tài chính.", note: "Hồ sơ phải có mô tả nhu cầu, dự toán chi phí và thời hạn dự kiến." },
  { page: 5, title: "02 / Thời hạn xử lý", quote: "Phòng tài chính phản hồi trong vòng 03 ngày làm việc kể từ khi nhận đủ hồ sơ hợp lệ.", note: "Nếu cần bổ sung chứng từ, thời hạn được tính lại từ ngày nhận đầy đủ tài liệu." },
];

export function CitationPreview() {
  const [activePage, setActivePage] = useState(2);
  const viewer = useRef<HTMLDivElement>(null);

  function selectPage(page: number) {
    setActivePage(page);
    const container = viewer.current;
    const target = container?.querySelector<HTMLElement>(`[data-page="${page}"]`);
    if (container && target) {
      container.scrollTo({ top: target.offsetTop - 20, behavior: "instant" });
    }
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-700 bg-[#0F172A] shadow-2xl shadow-cyan-950/20">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-700 px-5 py-4 text-xs text-slate-300">
        <span className="flex items-center gap-2"><span aria-hidden="true" className="size-2 rounded-full bg-cyan-300" /> Workspace / Vận hành doanh nghiệp</span>
        <span className="rounded-full border border-slate-600 px-2 py-1">Bản minh họa tương tác</span>
      </div>
      <div className="grid md:grid-cols-2">
        <div className="border-b border-slate-700 p-5 sm:p-7 md:border-r md:border-b-0">
          <h3 className="flex items-center gap-2 text-sm font-medium text-slate-300"><MessageSquareText aria-hidden="true" className="size-4 text-cyan-300" /> Hỏi đáp tài liệu</h3>
          <p className="mt-7 rounded-2xl rounded-tr-sm border border-slate-600 bg-slate-800 p-4 text-sm leading-7">Ai phê duyệt đề nghị mua sắm và mất bao lâu để xử lý?</p>
          <div className="mt-6 text-sm leading-7 text-slate-200">
            <p className="mb-3 font-semibold text-cyan-200">OmniDoc</p>
            <p>Đề nghị cần được <strong>trưởng bộ phận phê duyệt</strong> trước khi chuyển đến phòng tài chính.</p>
            <CitationButton page={2} active={activePage === 2} onClick={() => selectPage(2)} />
            <p className="mt-4">Phòng tài chính phản hồi trong <strong>03 ngày làm việc</strong> sau khi nhận đủ hồ sơ hợp lệ.</p>
            <CitationButton page={5} active={activePage === 5} onClick={() => selectPage(5)} />
          </div>
          <p className="mt-7 border-t border-slate-700 pt-4 text-xs leading-6 text-slate-400">Nhấn [Trang 2] hoặc [Trang 5] để đối soát nguồn. Nội dung minh họa từ tài liệu quy trình mẫu.</p>
        </div>
        <div className="min-w-0">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-700 px-5 py-4 text-xs text-slate-300">
            <span className="flex items-center gap-2"><FileText aria-hidden="true" className="size-4" /> Quy-trinh-mua-sam.pdf</span>
            <span role="status">Canonical PDF · Trang {activePage}</span>
          </div>
          <div ref={viewer} id="citation-preview-viewer" role="region" aria-label="Bản minh họa Canonical PDF" tabIndex={0} className="relative h-[420px] overflow-y-auto overscroll-contain bg-slate-950/50 p-5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-cyan-300">
            {pages.map(({ page, title, quote, note }) => (
              <article key={page} data-page={page} aria-label={`Trang ${page}`} className="mb-5 min-h-[380px] rounded-sm bg-slate-50 p-6 text-slate-800 shadow-lg sm:p-8">
                <p className="border-b border-slate-300 pb-4 text-[10px] font-semibold tracking-widest text-slate-600">OMNIDOC / QUY TRÌNH NỘI BỘ / MẪU</p>
                <h4 className="mt-7 text-xl font-semibold">{title}</h4>
                <p className="mt-6 text-sm leading-8"><mark className={activePage === page ? "bg-cyan-100 px-1 text-slate-950 ring-1 ring-cyan-600" : "bg-transparent text-slate-800"}>{quote}</mark></p>
                <p className="mt-5 text-sm leading-7 text-slate-600">{note}</p>
                <p className="mt-8 text-right text-xs text-slate-600">Trang {page}</p>
              </article>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function CitationButton({ page, active, onClick }: { page: number; active: boolean; onClick: () => void }) {
  return <button type="button" aria-pressed={active} aria-controls="citation-preview-viewer" onClick={onClick} className={`mt-2 inline-flex min-h-11 items-center rounded-lg border px-3 text-xs font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-900 ${active ? "border-cyan-300 bg-cyan-300 text-slate-950" : "border-cyan-700 bg-cyan-950 text-cyan-100 hover:bg-cyan-900"}`}>[Trang {page}]</button>;
}
