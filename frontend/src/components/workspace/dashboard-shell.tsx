"use client";

import type { ReactNode } from "react";

import { DashboardHeader } from "@/components/workspace/dashboard-header";

export function DashboardShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-50">
      <DashboardHeader />
      <main className="mx-auto max-w-[1440px] px-4 py-7 sm:px-6 lg:px-8">
        {children}
      </main>
    </div>
  );
}
