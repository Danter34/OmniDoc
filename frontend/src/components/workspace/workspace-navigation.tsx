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
      className="glass-panel mb-5 flex w-fit items-center gap-1 rounded-xl p-1"
    >
      {items.map((item) => {
        const Icon = item.icon;

        return (
          <Link
            aria-current={item.active ? "page" : undefined}
            className={cn(
              "inline-flex h-11 items-center gap-2 rounded-lg px-3 text-sm font-medium transition-[background-color,color,box-shadow] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-inset",
              item.active
                ? "active-gradient-item"
                : "text-muted hover:bg-surface-subtle hover:text-content",
            )}
            href={item.href}
            key={item.href}
          >
            <Icon
              aria-hidden="true"
              className={cn(
                "size-4",
                item.active && "drop-shadow-[0_0_5px_var(--sidebar-icon-glow)]",
              )}
            />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
