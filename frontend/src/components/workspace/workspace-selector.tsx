"use client";

import {
  Building2,
  Check,
  ChevronDown,
  FileText,
  Plus,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { CreateWorkspaceModal } from "@/components/workspace/create-workspace-modal";
import { useWorkspace } from "@/hooks/use-workspace";
import { cn } from "@/lib/utils";

export function WorkspaceSelector() {
  const router = useRouter();
  const {
    workspaces,
    activeWorkspace,
    activeWorkspaceId,
    setActiveWorkspaceId,
    isLoading,
  } = useWorkspace();
  const [open, setOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setOpen(false);
      }
    };

    window.addEventListener("pointerdown", handlePointerDown);
    return () => window.removeEventListener("pointerdown", handlePointerDown);
  }, []);

  function selectWorkspace(workspaceId: string) {
    setActiveWorkspaceId(workspaceId);
    setOpen(false);
    router.push(`/workspaces/${workspaceId}`);
  }

  return (
    <>
      <div className="relative" ref={containerRef}>
        <button
          aria-expanded={open}
          aria-haspopup="listbox"
          className="flex h-11 min-w-0 items-center gap-2.5 rounded-xl border border-slate-200 bg-white px-3 text-left shadow-sm transition hover:border-slate-300 hover:bg-slate-50 sm:min-w-64"
          disabled={isLoading}
          onClick={() => setOpen((current) => !current)}
          type="button"
        >
          <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
            <Building2 className="size-4" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-medium text-slate-800">
              {isLoading
                ? "Đang tải..."
                : (activeWorkspace?.name ?? "Chọn Workspace")}
            </span>
            {activeWorkspace ? (
              <span className="block text-[11px] text-slate-500">
                {activeWorkspace.role === "Member" ? "Member" : "Owner"}
              </span>
            ) : null}
          </span>
          <ChevronDown
            className={cn(
              "size-4 shrink-0 text-slate-400 transition-transform",
              open && "rotate-180",
            )}
          />
        </button>

        {open ? (
          <div className="absolute left-0 top-[calc(100%+8px)] z-40 w-[min(22rem,calc(100vw-2rem))] overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl shadow-slate-950/10">
            <div className="border-b border-slate-100 px-3 py-2.5">
              <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                Workspaces
              </p>
            </div>
            <div className="max-h-72 overflow-y-auto p-1.5" role="listbox">
              {workspaces.length > 0 ? (
                workspaces.map((workspace) => {
                  const selected = workspace.id === activeWorkspaceId;

                  return (
                    <button
                      aria-selected={selected}
                      className={cn(
                        "flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left transition",
                        selected
                          ? "bg-blue-50"
                          : "hover:bg-slate-50",
                      )}
                      key={workspace.id}
                      onClick={() => selectWorkspace(workspace.id)}
                      role="option"
                      type="button"
                    >
                      <span
                        className={cn(
                          "flex size-9 shrink-0 items-center justify-center rounded-xl",
                          selected
                            ? "bg-blue-100 text-blue-700"
                            : "bg-slate-100 text-slate-500",
                        )}
                      >
                        <Building2 className="size-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-slate-800">
                          {workspace.name}
                        </span>
                        <span className="mt-0.5 flex items-center gap-2 text-xs text-slate-500">
                          <span>
                            {workspace.role === "Member" ? "Member" : "Owner"}
                          </span>
                          <span className="text-slate-300">•</span>
                          <span className="inline-flex items-center gap-1">
                            <FileText className="size-3" />
                            {workspace.documentCount}
                          </span>
                        </span>
                      </span>
                      {selected ? (
                        <Check className="size-4 shrink-0 text-blue-600" />
                      ) : null}
                    </button>
                  );
                })
              ) : (
                <p className="px-3 py-5 text-center text-sm text-slate-500">
                  Chưa có workspace nào.
                </p>
              )}
            </div>
            <div className="border-t border-slate-100 p-1.5">
              <button
                className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-medium text-blue-600 transition hover:bg-blue-50"
                onClick={() => {
                  setOpen(false);
                  setCreateOpen(true);
                }}
                type="button"
              >
                <span className="flex size-9 items-center justify-center rounded-xl border border-dashed border-blue-300 bg-blue-50">
                  <Plus className="size-4" />
                </span>
                Tạo Workspace
              </button>
            </div>
          </div>
        ) : null}
      </div>

      <CreateWorkspaceModal
        onClose={() => setCreateOpen(false)}
        open={createOpen}
      />
    </>
  );
}
