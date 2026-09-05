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
  metadataBase: new URL(
    process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000",
  ),
  title: {
    default: "OmniDoc - Enterprise AI Document Platform",
    template: "%s | OmniDoc",
  },
  description:
    "Enterprise RAG & Document Intelligence Platform powered by .NET 10 and Gemini",
  icons: {
    icon: [
      { url: "/images/logo-icon.png", type: "image/png" },
      { url: "/icon.png", type: "image/png" },
    ],
    apple: "/images/logo-icon.png",
  },
  openGraph: {
    title: "OmniDoc - Enterprise AI Document Platform",
    description: "Intelligence in every document",
    images: [
      {
        url: "/images/logo-full.png",
        width: 1200,
        height: 630,
      },
    ],
  },
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
