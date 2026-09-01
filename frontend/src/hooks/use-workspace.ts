"use client";

import { useContext } from "react";

import { WorkspaceContext } from "@/components/workspace/workspace-provider";

export function useWorkspace() {
  const context = useContext(WorkspaceContext);

  if (!context) {
    throw new Error("useWorkspace must be used within a WorkspaceProvider.");
  }

  return context;
}
