"use client";

import { KeyRound, LogOut } from "lucide-react";
import { useState } from "react";

import { ChangePasswordModal } from "@/components/auth/change-password-modal";
import { NotificationBell } from "@/components/notifications/notification-bell";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { Logo } from "@/components/ui/logo";
import { WorkspaceSelector } from "@/components/workspace/workspace-selector";
import { useAuth } from "@/hooks/use-auth";
import { getInitials } from "@/lib/utils";

export function DashboardHeader() {
  const { user, logout } = useAuth();
  const [profileOpen, setProfileOpen] = useState(false);
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);

  return (
    <>
      <header className="glass-panel sticky top-0 z-30 border-x-0 border-t-0">
        <div className="mx-auto flex h-16 max-w-[var(--layout-max)] items-center gap-3 px-4 sm:px-6 lg:px-8">
          <Logo className="mr-1 shrink-0" />
          <div
            aria-hidden="true"
            className="hidden h-6 w-px bg-line-subtle sm:block"
          />
          <WorkspaceSelector />

          <div className="ml-auto flex items-center gap-1.5">
            <NotificationBell />
            <ThemeToggle />

            <div className="relative">
              <button
                aria-expanded={profileOpen}
                aria-haspopup="menu"
                aria-label={`Mở menu tài khoản của ${user?.fullName ?? "OmniDoc"}`}
                className="flex size-11 items-center justify-center rounded-full bg-info-subtle text-sm font-semibold text-accent ring-2 ring-surface transition-colors hover:bg-surface-tertiary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
                onClick={() => setProfileOpen((current) => !current)}
                type="button"
              >
                {getInitials(user?.fullName ?? "OD")}
              </button>

              {profileOpen ? (
                <div
                  className="glass-panel absolute right-0 top-[calc(100%+8px)] z-40 w-64 rounded-2xl p-2"
                  role="menu"
                >
                  <div className="border-b border-line-subtle px-3 py-2.5">
                    <p className="truncate text-sm font-medium text-content">
                      {user?.fullName}
                    </p>
                    <p className="mt-0.5 truncate text-xs text-muted">
                      {user?.email}
                    </p>
                  </div>
                  <button
                    className="mt-1 flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm text-content-secondary transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-inset"
                    onClick={() => {
                      setProfileOpen(false);
                      setChangePasswordOpen(true);
                    }}
                    role="menuitem"
                    type="button"
                  >
                    <KeyRound aria-hidden="true" className="size-4" />
                    Đổi mật khẩu
                  </button>
                  <button
                    className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm text-content-secondary transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-inset"
                    onClick={logout}
                    role="menuitem"
                    type="button"
                  >
                    <LogOut aria-hidden="true" className="size-4" />
                    Đăng xuất
                  </button>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      </header>
      {changePasswordOpen ? (
        <ChangePasswordModal onClose={() => setChangePasswordOpen(false)} />
      ) : null}
    </>
  );
}
