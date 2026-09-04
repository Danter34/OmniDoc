"use client";

import { KeyRound, LogOut } from "lucide-react";
import { useState } from "react";

import { ChangePasswordModal } from "@/components/auth/change-password-modal";
import { NotificationBell } from "@/components/notifications/notification-bell";
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
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-[1440px] items-center gap-3 px-4 sm:px-6 lg:px-8">
          <Logo className="mr-1 shrink-0" />
          <div className="hidden h-6 w-px bg-slate-200 sm:block" />
          <WorkspaceSelector />

          <div className="ml-auto flex items-center gap-1.5">
            <NotificationBell />

            <div className="relative">
              <button
                aria-expanded={profileOpen}
                aria-haspopup="menu"
                className="flex size-10 items-center justify-center rounded-full bg-blue-100 text-sm font-semibold text-blue-700 ring-2 ring-white transition hover:bg-blue-200"
                onClick={() => setProfileOpen((current) => !current)}
                type="button"
              >
                {getInitials(user?.fullName ?? "OD")}
              </button>

              {profileOpen ? (
                <div
                  className="absolute right-0 top-[calc(100%+8px)] z-40 w-64 rounded-2xl border border-slate-200 bg-white p-2 shadow-xl shadow-slate-950/10"
                  role="menu"
                >
                  <div className="border-b border-slate-100 px-3 py-2.5">
                    <p className="truncate text-sm font-medium text-slate-900">
                      {user?.fullName}
                    </p>
                    <p className="mt-0.5 truncate text-xs text-slate-500">
                      {user?.email}
                    </p>
                  </div>
                  <button
                    className="mt-1 flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm text-slate-600 transition hover:bg-slate-50 hover:text-slate-900"
                    onClick={() => {
                      setProfileOpen(false);
                      setChangePasswordOpen(true);
                    }}
                    role="menuitem"
                    type="button"
                  >
                    <KeyRound className="size-4" />
                    Đổi mật khẩu
                  </button>
                  <button
                    className="flex w-full items-center gap-2.5 rounded-xl px-3 py-2.5 text-sm text-slate-600 transition hover:bg-slate-50 hover:text-slate-900"
                    onClick={logout}
                    role="menuitem"
                    type="button"
                  >
                    <LogOut className="size-4" />
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
