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
          className="flex h-11 min-w-0 items-center gap-2.5 rounded-xl border border-line-subtle bg-surface/80 px-3 text-left shadow-sm transition-[background-color,border-color,box-shadow] hover:border-line hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring sm:min-w-64"
          disabled={isLoading}
          onClick={() => setOpen((current) => !current)}
          type="button"
        >
          <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-info-subtle text-accent">
            <Building2 className="size-4" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-medium text-content">
              {isLoading
                ? "Đang tải..."
                : (activeWorkspace?.name ?? "Chọn Workspace")}
            </span>
            {activeWorkspace ? (
              <span className="block text-[11px] text-muted">
                {activeWorkspace.role === "Member" ? "Member" : "Owner"}
              </span>
            ) : null}
          </span>
          <ChevronDown
            className={cn(
              "size-4 shrink-0 text-muted transition-transform",
              open && "rotate-180",
            )}
          />
        </button>

        {open ? (
          <div className="glass-panel absolute left-0 top-[calc(100%+8px)] z-40 w-[min(22rem,calc(100vw-2rem))] overflow-hidden rounded-2xl">
            <div className="border-b border-line-subtle px-3 py-2.5">
              <p className="text-xs font-semibold uppercase tracking-wider text-muted">
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
                          ? "active-gradient-item"
                          : "text-content-secondary hover:bg-surface-subtle",
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
                            ? "bg-info-subtle text-accent"
                            : "bg-surface-tertiary text-muted",
                        )}
                      >
                        <Building2 className="size-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-content">
                          {workspace.name}
                        </span>
                        <span className="mt-0.5 flex items-center gap-2 text-xs text-muted">
                          <span>
                            {workspace.role === "Member" ? "Member" : "Owner"}
                          </span>
                          <span className="text-line-strong">•</span>
                          <span className="inline-flex items-center gap-1">
                            <FileText className="size-3" />
                            {workspace.documentCount}
                          </span>
                        </span>
                      </span>
                      {selected ? (
                        <Check className="size-4 shrink-0 text-accent" />
                      ) : null}
                    </button>
                  );
                })
              ) : (
                <p className="px-3 py-5 text-center text-sm text-muted">
                  Chưa có workspace nào.
                </p>
              )}
            </div>
            <div className="border-t border-line-subtle p-1.5">
              <button
                className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left text-sm font-medium text-accent transition-colors hover:bg-info-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-inset"
                onClick={() => {
                  setOpen(false);
                  setCreateOpen(true);
                }}
                type="button"
              >
                <span className="flex size-9 items-center justify-center rounded-xl border border-dashed border-line-strong bg-info-subtle">
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
