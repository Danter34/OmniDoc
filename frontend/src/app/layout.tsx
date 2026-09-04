import type { Metadata } from "next";
import type { ReactNode } from "react";

import { AuthProvider } from "@/components/auth/auth-provider";
import { ThemeProvider } from "@/components/theme/theme-provider";

import "./globals.css";

const themeBootstrapScript = `(() => {
  const storageKey = "omnidoc.theme";
  const cookieKey = "omnidoc-theme";
  const isTheme = (value) => value === "system" || value === "light" || value === "dark";
  let preference = null;

  try {
    const storedTheme = window.localStorage.getItem(storageKey);
    if (isTheme(storedTheme)) preference = storedTheme;
  } catch {}

  if (!preference) {
    const cookie = document.cookie
      .split(";")
      .map((part) => part.trim())
      .find((part) => part.startsWith(cookieKey + "="));
    const cookieTheme = cookie ? cookie.slice(cookieKey.length + 1) : null;
    if (isTheme(cookieTheme)) preference = cookieTheme;
  }

  if (!preference) preference = "system";
  const resolvedTheme = preference === "system"
    ? (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light")
    : preference;
  const root = document.documentElement;
  root.dataset.theme = resolvedTheme;
  root.style.colorScheme = resolvedTheme;
})();`;

export const metadata: Metadata = {
  title: {
    default: "OmniDoc",
    template: "%s · OmniDoc",
  },
  description:
    "Không gian tri thức thông minh cho tài liệu PDF doanh nghiệp.",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="vi" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeBootstrapScript }} />
      </head>
      <body className="ambient-bg">
        <ThemeProvider>
          <AuthProvider>{children}</AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
