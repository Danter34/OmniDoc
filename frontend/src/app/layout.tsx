import type { Metadata } from "next";
import { Inter } from "next/font/google";
import type { ReactNode } from "react";

import { AuthProvider } from "@/components/auth/auth-provider";
import { ThemeProvider } from "@/components/theme/theme-provider";
import { SESSION_COOKIE, SESSION_COOKIE_MAX_AGE, TOKEN_STORAGE_KEY } from "@/lib/session";

import "./globals.css";

const inter = Inter({
  subsets: ["latin", "vietnamese"],
  display: "swap",
  variable: "--font-inter",
});

// Migrate sessions created before server-readable cookies existed, before painting
// the landing page. The subsequent server request still validates the token.
const sessionMigrationScript = `(() => {
  if (window.location.pathname !== "/") return;
  try {
    const cookieName = ${JSON.stringify(SESSION_COOKIE)};
    if (document.cookie.split(";").some(part => part.trim().startsWith(cookieName + "="))) return;
    const token = window.localStorage.getItem(${JSON.stringify(TOKEN_STORAGE_KEY)});
    if (!token) return;
    const secure = window.location.protocol === "https:" ? "; Secure" : "";
    document.cookie = cookieName + "=" + encodeURIComponent(token) + "; Path=/; Max-Age=${SESSION_COOKIE_MAX_AGE}; SameSite=Lax" + secure;
    if (document.cookie.split(";").some(part => part.trim().startsWith(cookieName + "="))) {
      document.documentElement.style.visibility = "hidden";
      window.location.reload();
    }
  } catch {}
})();`;

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
    <html lang="vi" className={inter.variable} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: sessionMigrationScript }} />
        <script dangerouslySetInnerHTML={{ __html: themeBootstrapScript }} />
      </head>
      <body className="ambient-bg font-sans antialiased">
        <ThemeProvider>
          <AuthProvider>{children}</AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
