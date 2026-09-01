"use client";

import { AlertCircle, RefreshCw } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { WorkspaceEmptyState } from "@/components/workspace/workspace-empty-state";
import { useWorkspace } from "@/hooks/use-workspace";

export default function WorkspacesPage() {
  const router = useRouter();
  const {
    activeWorkspaceId,
    workspaces,
    isLoading,
    error,
    refreshWorkspaces,
  } = useWorkspace();

  useEffect(() => {
    if (!isLoading && activeWorkspaceId) {
      router.replace(`/workspaces/${activeWorkspaceId}`);
    }
  }, [activeWorkspaceId, isLoading, router]);

  if (isLoading || activeWorkspaceId) {
    return (
      <div className="flex min-h-[calc(100vh-9rem)] items-center justify-center">
        <div className="flex items-center gap-3 text-sm text-slate-500">
          <Spinner className="size-5 text-blue-600" />
          Đang mở workspace...
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex min-h-[calc(100vh-9rem)] items-center justify-center">
        <div className="max-w-md rounded-2xl border border-rose-200 bg-white p-6 text-center shadow-sm">
          <AlertCircle className="mx-auto size-8 text-rose-500" />
          <h1 className="mt-3 font-semibold text-slate-900">
            Không thể tải Workspace
          </h1>
          <p className="mt-2 text-sm leading-6 text-slate-500">{error}</p>
          <Button
            className="mt-5"
            icon={<RefreshCw className="size-4" />}
            onClick={() => void refreshWorkspaces()}
            variant="secondary"
          >
            Thử lại
          </Button>
        </div>
      </div>
    );
  }

  if (workspaces.length === 0) {
    return <WorkspaceEmptyState />;
  }

  return null;
}
