"use client";

import {
  createContext,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

import { useAuth } from "@/hooks/use-auth";
import { getErrorMessage } from "@/services/api-client";
import { workspaceService } from "@/services/workspace.service";
import type {
  CreateWorkspaceRequest,
  Workspace,
} from "@/types/workspace.types";

const ACTIVE_WORKSPACE_KEY = "omnidoc.activeWorkspaceId";

interface WorkspaceContextValue {
  workspaces: Workspace[];
  activeWorkspace: Workspace | null;
  activeWorkspaceId: string | null;
  isLoading: boolean;
  error: string | null;
  setActiveWorkspaceId: (workspaceId: string) => void;
  incrementDocumentCount: (workspaceId: string) => void;
  createWorkspace: (payload: CreateWorkspaceRequest) => Promise<Workspace>;
  refreshWorkspaces: () => Promise<void>;
}

export const WorkspaceContext = createContext<
  WorkspaceContextValue | undefined
>(undefined);

function normalizeWorkspace(workspace: Workspace): Workspace {
  return {
    ...workspace,
    // The current API does not serialize Role yet. Existing creation and access
    // flows create owned workspaces, while the optional field is forward-compatible.
    role: workspace.role ?? "Owner",
  };
}

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [activeWorkspaceId, setActiveWorkspaceIdState] = useState<string | null>(
    null,
  );
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const setActiveWorkspaceId = useCallback((workspaceId: string) => {
    setActiveWorkspaceIdState(workspaceId);
    window.localStorage.setItem(ACTIVE_WORKSPACE_KEY, workspaceId);
  }, []);

  const storeWorkspaces = useCallback((items: Workspace[]) => {
    const normalizedItems = items.map(normalizeWorkspace);
    setWorkspaces(normalizedItems);
    setActiveWorkspaceIdState((current) => {
      const stored = window.localStorage.getItem(ACTIVE_WORKSPACE_KEY);
      const candidate = current ?? stored;
      const nextId =
        candidate &&
        normalizedItems.some((item) => item.id === candidate)
          ? candidate
          : (normalizedItems[0]?.id ?? null);

      if (nextId) {
        window.localStorage.setItem(ACTIVE_WORKSPACE_KEY, nextId);
      } else {
        window.localStorage.removeItem(ACTIVE_WORKSPACE_KEY);
      }

      return nextId;
    });
  }, []);

  const refreshWorkspaces = useCallback(async () => {
    if (!user) {
      setWorkspaces([]);
      setActiveWorkspaceIdState(null);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      storeWorkspaces(await workspaceService.getAll());
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsLoading(false);
    }
  }, [storeWorkspaces, user]);

  useEffect(() => {
    if (!user) {
      return;
    }

    const controller = new AbortController();

    workspaceService
      .getAll(controller.signal)
      .then((items) => {
        storeWorkspaces(items);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (
          requestError instanceof DOMException &&
          requestError.name === "AbortError"
        ) {
          return;
        }

        setError(getErrorMessage(requestError));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [storeWorkspaces, user]);

  const createWorkspace = useCallback(
    async (payload: CreateWorkspaceRequest) => {
      const created = normalizeWorkspace(await workspaceService.create(payload));
      setWorkspaces((current) => [created, ...current]);
      setActiveWorkspaceId(created.id);
      return created;
    },
    [setActiveWorkspaceId],
  );

  const incrementDocumentCount = useCallback((workspaceId: string) => {
    setWorkspaces((current) =>
      current.map((workspace) =>
        workspace.id === workspaceId
          ? { ...workspace, documentCount: workspace.documentCount + 1 }
          : workspace,
      ),
    );
  }, []);

  const activeWorkspace = useMemo(
    () =>
      workspaces.find((workspace) => workspace.id === activeWorkspaceId) ?? null,
    [activeWorkspaceId, workspaces],
  );

  const value = useMemo<WorkspaceContextValue>(
    () => ({
      workspaces,
      activeWorkspace,
      activeWorkspaceId,
      isLoading,
      error,
      setActiveWorkspaceId,
      incrementDocumentCount,
      createWorkspace,
      refreshWorkspaces,
    }),
    [
      activeWorkspace,
      activeWorkspaceId,
      createWorkspace,
      error,
      incrementDocumentCount,
      isLoading,
      refreshWorkspaces,
      setActiveWorkspaceId,
      workspaces,
    ],
  );

  return (
    <WorkspaceContext.Provider value={value}>
      {children}
    </WorkspaceContext.Provider>
  );
}
