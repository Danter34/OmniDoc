"use client";

import { Files, MessageSquareText, Settings } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { useWorkspace } from "@/hooks/use-workspace";
import { cn } from "@/lib/utils";

export function WorkspaceNavigation() {
  const pathname = usePathname();
  const { activeWorkspaceId } = useWorkspace();

  if (!activeWorkspaceId) {
    return null;
  }

  const items = [
    {
      href: `/workspaces/${activeWorkspaceId}`,
      label: "Tài liệu",
      icon: Files,
      active: pathname === `/workspaces/${activeWorkspaceId}`,
    },
    {
      href: `/workspaces/${activeWorkspaceId}/chat`,
      label: "Trò chuyện",
      icon: MessageSquareText,
      active: pathname.startsWith(`/workspaces/${activeWorkspaceId}/chat`),
    },
    {
      href: `/workspaces/${activeWorkspaceId}/settings`,
      label: "Cài đặt",
      icon: Settings,
      active: pathname.startsWith(`/workspaces/${activeWorkspaceId}/settings`),
    },
  ];

  return (
    <nav
      aria-label="Điều hướng Workspace"
      className="mb-5 flex w-fit items-center gap-1 rounded-lg border border-line-subtle bg-surface-subtle p-1"
    >
      {items.map((item) => {
        const Icon = item.icon;

        return (
          <Link
            aria-current={item.active ? "page" : undefined}
            className={cn(
              "inline-flex h-11 items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-inset",
              item.active
                ? "active-gradient-item"
                : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white",
            )}
            href={item.href}
            key={item.href}
          >
            <Icon aria-hidden="true" className="size-4" />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
