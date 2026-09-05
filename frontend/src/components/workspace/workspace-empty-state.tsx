"use client";

import { ArrowRight, FilePlus2, FolderPlus, Sparkles } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { CreateWorkspaceModal } from "@/components/workspace/create-workspace-modal";

export function WorkspaceEmptyState() {
  const [open, setOpen] = useState(false);

  return (
    <>
      <div className="mx-auto flex min-h-[calc(100vh-12rem)] max-w-3xl items-center justify-center px-4 py-12">
        <section className="glass-panel w-full overflow-hidden rounded-3xl">
          <div className="ambient-bg relative border-b border-line-subtle px-6 py-12 text-center sm:px-12">
            <div className="absolute left-10 top-8 text-accent opacity-40">
              <Sparkles className="size-6" />
            </div>
            <div className="absolute bottom-8 right-10 text-citation opacity-60">
              <Sparkles className="size-5" />
            </div>
            <span className="mx-auto flex size-16 items-center justify-center rounded-2xl text-brand-icon shadow-[var(--accent-glow)] [background-image:var(--gradient-brand)]">
              <FolderPlus className="size-8" />
            </span>
            <h1 className="mt-6 text-2xl font-semibold tracking-tight text-content">
              Tạo Workspace đầu tiên
            </h1>
            <p className="mx-auto mt-3 max-w-lg text-sm leading-6 text-content-secondary">
              Workspace giúp bạn tổ chức PDF theo dự án, phòng ban hoặc nhóm tri
              thức riêng biệt.
            </p>
            <Button
              className="mt-7"
              icon={<ArrowRight className="size-4" />}
              onClick={() => setOpen(true)}
              size="lg"
            >
              Bắt đầu ngay
            </Button>
          </div>
          <div className="grid gap-px bg-line-subtle sm:grid-cols-2">
            <div className="bg-surface p-6">
              <span className="flex size-10 items-center justify-center rounded-xl bg-info-subtle text-accent">
                <FilePlus2 className="size-5" />
              </span>
              <h2 className="mt-4 text-sm font-semibold text-content">
                Tải lên tài liệu
              </h2>
              <p className="mt-1.5 text-sm leading-6 text-muted">
                Kéo thả PDF và theo dõi quá trình lập chỉ mục theo thời gian thực.
              </p>
            </div>
            <div className="bg-surface p-6">
              <span className="flex size-10 items-center justify-center rounded-xl bg-warning-subtle text-warning">
                <Sparkles className="size-5" />
              </span>
              <h2 className="mt-4 text-sm font-semibold text-content">
                Sẵn sàng cho RAG
              </h2>
              <p className="mt-1.5 text-sm leading-6 text-muted">
                Tài liệu được xử lý thành nguồn tri thức có thể hỏi đáp và trích dẫn.
              </p>
            </div>
          </div>
        </section>
      </div>

      <CreateWorkspaceModal onClose={() => setOpen(false)} open={open} />
    </>
  );
}
