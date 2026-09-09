"use client";

import { FileText, MessageSquareText } from "lucide-react";
import { useRef, useState } from "react";

import { SHOWCASE_DEFAULT_DOCUMENT } from "@/lib/showcase";

// Selected rows from the forecast table on physical PDF page 2 (printed page 42).
const forecasts = [
  { economy: "ASEAN+3", gdp: ["4.5", "4.2"], inflation: ["4.3", "3.7"] },
  { economy: "Trung Quốc", gdp: ["5.3", "4.9"], inflation: ["1.0", "1.6"] },
  { economy: "Indonesia", gdp: ["5.2", "5.2"], inflation: ["2.8", "2.5"] },
  { economy: "Thái Lan", gdp: ["2.9", "3.1"], inflation: ["1.2", "1.9"] },
  { economy: "Việt Nam", gdp: ["6.0", "6.5"], inflation: ["3.6", "2.7"] },
];

export function CitationPreview() {
  const [citationSelected, setCitationSelected] = useState(false);
  const viewer = useRef<HTMLDivElement>(null);
  const vietnamRow = useRef<HTMLTableRowElement>(null);

  function selectCitation() {
    setCitationSelected(true);
    const container = viewer.current;
    const target = vietnamRow.current;
    if (container && target) {
      const top = target.getBoundingClientRect().top - container.getBoundingClientRect().top + container.scrollTop;
      container.scrollTo({ top: Math.max(0, top - container.clientHeight / 2), behavior: "instant" });
      container.focus({ preventScroll: true });
    }
  }

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-700 bg-[#0F172A] shadow-2xl shadow-cyan-950/20">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-700 px-5 py-4 text-xs text-slate-300">
        <span className="flex items-center gap-2"><span aria-hidden="true" className="size-2 rounded-full bg-cyan-300" /> Workspace / Báo cáo ASEAN+3 — AMRO</span>
        <span className="rounded-full border border-slate-600 px-2 py-1">Bản minh họa tương tác</span>
      </div>
      <div className="grid md:grid-cols-2">
        <div className="border-b border-slate-700 p-5 sm:p-7 md:border-r md:border-b-0">
          <h3 className="flex items-center gap-2 text-sm font-medium text-slate-300"><MessageSquareText aria-hidden="true" className="size-4 text-cyan-300" /> Hỏi đáp tài liệu</h3>
          <p className="mt-7 rounded-2xl rounded-tr-sm border border-slate-600 bg-slate-800 p-4 text-sm leading-7">Dự báo tăng trưởng GDP của Việt Nam năm 2024 và 2025 theo báo cáo AMRO là bao nhiêu?</p>
          <div className="mt-6 text-sm leading-7 text-slate-200">
            <p className="mb-3 font-semibold"><span className="text-white">Omni</span><span className="bg-clip-text text-transparent" style={{ backgroundImage: "linear-gradient(90deg,#00f0ff 0%,#3b82f6 45%,#a855f7 100%)" }}>Doc</span></p>
            <p>Theo Báo cáo Triển vọng Kinh tế Khu vực ASEAN+3 2024 (AMRO), tăng trưởng GDP của Việt Nam được dự báo đạt <strong>6,0% trong năm 2024</strong> và tăng lên <strong>6,5% trong năm 2025</strong>.</p>
            <button type="button" aria-pressed={citationSelected} aria-controls="citation-preview-viewer" onClick={selectCitation} className={`mt-2 inline-flex min-h-11 items-center rounded-lg border px-3 text-xs font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-900 ${citationSelected ? "border-cyan-300 bg-cyan-300 text-slate-950" : "border-cyan-700 bg-cyan-950 text-cyan-100 hover:bg-cyan-900"}`}>[Trang 2]</button>
          </div>
          <p className="mt-7 border-t border-slate-700 pt-4 text-xs leading-6 text-slate-400">Nhấn [Trang 2] để đối soát hàng số liệu Việt Nam. Đây là dự báo trong báo cáo AMRO 2024.</p>
        </div>
        <div className="min-w-0">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-700 px-5 py-4 text-xs text-slate-300">
            <span className="flex min-w-0 items-center gap-2"><FileText aria-hidden="true" className="size-4 shrink-0" /><span className="break-all">{SHOWCASE_DEFAULT_DOCUMENT}</span></span>
            <span role="status">{citationSelected ? "Đã chọn Việt Nam · " : ""}Trang 2 / 4</span>
          </div>
          <div ref={viewer} id="citation-preview-viewer" role="region" aria-label="Bản minh họa trang 2 báo cáo AMRO" tabIndex={0} className="relative h-[420px] overflow-auto overscroll-contain bg-slate-950/50 p-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-cyan-300 sm:p-5">
            <article aria-label="Trang 2" className="min-h-[380px] rounded-sm bg-slate-50 p-4 text-slate-800 shadow-lg sm:p-6">
              <p className="border-b border-slate-300 pb-4 text-[10px] font-semibold tracking-widest text-slate-600">ASEAN+3 REGIONAL ECONOMIC OUTLOOK 2024</p>
              <h4 className="mt-5 text-lg font-semibold">Ước tính và Dự báo Tăng trưởng và Lạm phát của AMRO, 2024–2025</h4>
              <div className="mt-4 overflow-x-auto">
                <table className="w-full min-w-[300px] border-collapse text-left text-xs">
                  <caption className="mb-3 text-left text-slate-600">Một số nền kinh tế · % so với cùng kỳ năm trước</caption>
                  <thead>
                    <tr className="border-b border-slate-300"><th scope="col" rowSpan={2} className="p-2">Nền kinh tế</th><th scope="colgroup" colSpan={2} className="p-2 text-center">GDP</th><th scope="colgroup" colSpan={2} className="p-2 text-center">Lạm phát</th></tr>
                    <tr className="border-b border-slate-300">{["GDP", "Lạm phát"].flatMap((metric) => [2024, 2025].map((year) => <th key={`${metric}-${year}`} scope="col" className="p-2 text-right">{year}f</th>))}</tr>
                  </thead>
                  <tbody>
                    {forecasts.map(({ economy, gdp, inflation }) => (
                      <tr key={economy} ref={economy === "Việt Nam" ? vietnamRow : undefined} className={economy === "Việt Nam" ? `border-y-2 border-cyan-600 font-semibold text-slate-950 ${citationSelected ? "bg-cyan-200" : "bg-cyan-100"}` : "border-b border-slate-200"}>
                        <th scope="row" className="p-2">{economy}</th>
                        {[...gdp, ...inflation].map((value, index) => <td key={index} className="p-2 text-right tabular-nums">{value}</td>)}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="mt-5 text-[10px] leading-5 text-slate-600">Nguồn: Các cơ quan trong nước thông qua CEIC và Haver Analytics; Ước tính và dự báo của AMRO. f = dự báo.</p>
              <p className="mt-4 text-right text-xs text-slate-600">Trang 2</p>
            </article>
          </div>
        </div>
      </div>
    </div>
  );
}
