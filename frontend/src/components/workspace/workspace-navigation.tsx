"use client";

import { Files, MessageSquareText } from "lucide-react";
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
  ];

  return (
    <nav className="mb-5 flex w-fit items-center gap-1 rounded-xl border border-slate-200 bg-white p-1 shadow-sm">
      {items.map((item) => {
        const Icon = item.icon;

        return (
          <Link
            className={cn(
              "inline-flex h-9 items-center gap-2 rounded-lg px-3 text-sm font-medium transition",
              item.active
                ? "bg-blue-50 text-blue-700"
                : "text-slate-500 hover:bg-slate-50 hover:text-slate-800",
            )}
            href={item.href}
            key={item.href}
          >
            <Icon className="size-4" />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
