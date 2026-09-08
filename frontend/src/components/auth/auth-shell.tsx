"use client";

import type { ReactNode } from "react";

/**
 * Wraps every auth/onboarding page in a dark-themed surface.
 *
 * Strategy: instead of touching the global <html data-theme>, we place
 * [data-theme="dark"] on the outermost div so Tailwind / CSS variables
 * resolve to their dark values inside this subtree, while the workspace
 * pages continue to use the user's chosen theme preference freely.
 */
export function AuthShell({ children }: { children: ReactNode }) {
  return (
    <div
      data-theme="dark"
      // Hardcode the canvas colour so no flash occurs even before CSS vars kick in.
      className="min-h-screen bg-[#090d16]"
      style={{ colorScheme: "dark" }}
    >
      {children}
    </div>
  );
}
