import { ArrowRight, Database, FileStack, Code2, Layers3, Radio, ScanText } from "lucide-react";
import Link from "next/link";

import { CitationPreview } from "@/components/landing/citation-preview";
import { showcase } from "@/lib/showcase";

const highlights = [
  {
    icon: Database,
    title: "Một nền tảng, ranh giới rõ ràng",
    stack: ".NET 10 · Clean Architecture",
    description: "Domain và Application tách khỏi hạ tầng. EF Core, PostgreSQL 17 và pgvector 768 chiều giữ dữ liệu nghiệp vụ cùng vector trong một hệ quản trị, giảm độ phức tạp vận hành.",
    detail: "CORE BACKEND",
  },
  {
    icon: FileStack,
    title: "Nhiều định dạng. Một bản đối soát.",
    stack: "Gotenberg v8 · Canonical PDF",
    description: "LibreOffice và Chromium chuẩn hóa PDF, DOCX, PPTX, TXT, MD về Canonical PDF. Một Single Source of Truth cho hiển thị và đối soát citation; đổi lại là bước chuyển đổi trước khi lập chỉ mục.",
    detail: "INGESTION ENGINE",
  },
  {
    icon: Radio,
    title: "Xử lý nền, tiến độ trước mắt",
    stack: "Hangfire · SignalR",
    description: "Hangfire hỗ trợ worker phân tán để xử lý tác vụ trích xuất ngoài request. SignalR cập nhật tiến độ thời gian thực, giúp người dùng biết tài liệu đang ở bước nào.",
    detail: "BACKGROUND PROCESSING",
  },
];

const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-300 focus-visible:ring-offset-4 focus-visible:ring-offset-[#0B0F17]";

export function PortfolioLanding() {
  return (
    <div className="min-h-screen bg-[#0B0F17] text-slate-100 selection:bg-cyan-300 selection:text-slate-950">
      <a href="#main-content" className={`sr-only z-50 rounded-lg bg-slate-950 p-4 text-white focus:not-sr-only focus:absolute ${focus}`}>
        Chuyển đến nội dung chính
      </a>
      <header className="relative mx-auto flex max-w-7xl items-center justify-between gap-4 px-5 py-6 sm:px-8">
        <Link href="/" aria-label="OmniDoc — Trang chủ" className={`flex items-center gap-2 rounded-lg text-xl font-semibold tracking-tight ${focus}`}>
          <Layers3 aria-hidden="true" className="size-8 text-cyan-300" /> OmniDoc<span className="text-cyan-300">.</span>
        </Link>
        <nav aria-label="Điều hướng chính" className="flex items-center gap-6 text-sm">
          <a href="#architecture" className={`hidden rounded text-slate-300 hover:text-white sm:inline-flex ${focus}`}>Kiến trúc</a>
          <Link href="/login" className={`inline-flex min-h-11 items-center rounded-lg border border-slate-600 px-4 hover:bg-slate-800 ${focus}`}>Đăng nhập <ArrowRight aria-hidden="true" className="ml-2 size-4" /></Link>
        </nav>
      </header>

      <main id="main-content">
        <section aria-labelledby="hero-title" className="relative isolate overflow-hidden px-5 pb-16 pt-16 sm:px-8 sm:pt-24">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_20%_20%,#164e634d,transparent_50%),radial-gradient(ellipse_at_85%_45%,#7c3aed26,transparent_50%)]" />
          <div className="mx-auto max-w-7xl">
            <p className="mb-6 inline-flex items-center gap-2 rounded-full border border-cyan-300/30 bg-cyan-300/5 px-4 py-2 text-xs font-semibold tracking-widest text-cyan-200">
              <ScanText aria-hidden="true" className="size-4" /> EVIDENCE FIRST. INTELLIGENCE NEXT.
            </p>
            <h1 id="hero-title" className="max-w-5xl text-4xl font-semibold leading-[1.12] tracking-tight sm:text-6xl lg:text-7xl">
              OmniDoc — Enterprise RAG &amp; <span className="bg-linear-to-r from-cyan-200 via-blue-300 to-violet-300 bg-clip-text text-transparent">Document Intelligence</span>
            </h1>
            <p className="mt-7 max-w-2xl text-base leading-8 text-slate-300 sm:text-lg">
              Đối mặt với bài toán ảo giác (Hallucination) trong hỏi đáp tài liệu doanh nghiệp bằng bằng chứng có thể kiểm tra. Dual-Pane Citation đặt câu trả lời và tài liệu nguồn song song, giúp bạn đối soát từng nhận định ngay tại trang được trích dẫn.
            </p>
            <div className="mt-9 flex flex-wrap gap-3">
              <Link href={showcase.enabled ? "/login?mode=showcase" : "/login"} className={`inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-linear-to-r from-blue-700 to-violet-700 px-6 font-semibold text-white hover:brightness-125 ${focus}`}>
                Trải nghiệm ngay <ArrowRight aria-hidden="true" className="size-4" />
              </Link>
              <Link href="/register" className={`inline-flex min-h-12 items-center justify-center rounded-xl border border-slate-600 bg-slate-900 px-6 font-medium hover:bg-slate-800 ${focus}`}>Đăng ký tài khoản</Link>
            </div>
            <p className="mt-4 text-sm leading-6 text-slate-400">
              {showcase.enabled ? "Có tài khoản trải nghiệm điền sẵn tại màn hình đăng nhập. " : ""}
              Đăng ký riêng để khám phá chu trình xác minh email &amp; OTP.
            </p>
            <div className="mt-12 flex flex-wrap gap-x-8 gap-y-3 border-t border-slate-800 pt-6 text-sm text-slate-300">
              <span>PDF / DOCX / PPTX / TXT / MD</span><span>Streaming chat</span><span>Citation theo trang</span><span>Tiến độ realtime</span>
            </div>
          </div>
        </section>

        <section aria-labelledby="preview-title" className="mx-auto max-w-7xl px-5 py-12 sm:px-8">
          <div className="mb-8 max-w-2xl">
            <p className="text-xs font-semibold tracking-widest text-cyan-300">DUAL-PANE CITATION</p>
            <h2 id="preview-title" className="mt-3 text-3xl font-semibold tracking-tight sm:text-4xl">Câu trả lời đi cùng bằng chứng.</h2>
            <p className="mt-4 leading-7 text-slate-300">Chọn một badge citation để chuyển đến trang và đoạn trích tương ứng. Minh họa tương tác dưới đây dùng dữ liệu mẫu, không gọi AI.</p>
          </div>
          <CitationPreview />
        </section>

        <section id="architecture" aria-labelledby="architecture-title" className="mx-auto max-w-7xl scroll-mt-8 px-5 py-16 sm:px-8">
          <p className="text-xs font-semibold tracking-widest text-violet-300">ARCHITECTURE, WITH INTENT</p>
          <h2 id="architecture-title" className="mt-3 text-3xl font-semibold tracking-tight sm:text-4xl">Thiết kế để giải quyết bài toán thực.</h2>
          <div className="mt-9 grid gap-5 lg:grid-cols-3">
            {highlights.map(({ icon: Icon, title, stack, description, detail }) => (
              <article key={detail} className="rounded-2xl border border-slate-700 bg-linear-to-br from-[#0F172A] to-[#0B0F17] p-7">
                <Icon aria-hidden="true" className="mb-6 size-8 text-cyan-300" />
                <p className="text-xs tracking-widest text-slate-400">{detail}</p>
                <h3 className="mt-3 text-xl font-semibold">{title}</h3>
                <p className="mt-3 text-sm font-medium text-violet-300">{stack}</p>
                <p className="mt-4 text-sm leading-7 text-slate-300">{description}</p>
              </article>
            ))}
          </div>
          <a href="https://github.com/Danter34/OmniDoc#readme" className={`mt-7 inline-flex min-h-11 items-center gap-2 rounded text-sm text-cyan-200 underline underline-offset-4 ${focus}`}>Khám phá mã nguồn &amp; tài liệu kiến trúc <ArrowRight aria-hidden="true" className="size-4" /></a>
        </section>
      </main>

      <footer className="border-t border-slate-800 px-5 py-8 sm:px-8">
        <div className="mx-auto flex max-w-7xl flex-col gap-5 text-sm text-slate-400 sm:flex-row sm:items-center sm:justify-between">
          <div><p className="text-slate-200">OmniDoc · Portfolio Engineering Showcase</p><p className="mt-2">© {new Date().getFullYear()} OmniDoc. Từ tài liệu đến tri thức có thể đối soát.</p></div>
          <div className="flex items-center gap-5">
            <a href="https://github.com/Danter34/OmniDoc/releases/tag/v1.1.0" className={`rounded-full border border-slate-700 px-3 py-2 text-slate-300 hover:text-white ${focus}`}>v1.1.0</a>
            <a href="https://github.com/Danter34/OmniDoc" className={`inline-flex min-h-11 items-center gap-2 rounded text-slate-200 hover:text-cyan-200 ${focus}`}><Code2 aria-hidden="true" className="size-4" /> GitHub</a>
          </div>
        </div>
      </footer>
    </div>
  );
}
