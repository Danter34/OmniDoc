import type { Metadata } from "next";
import type { ReactNode } from "react";

import { AuthProvider } from "@/components/auth/auth-provider";

import "./globals.css";

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
    <html lang="vi">
      <body>
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}
