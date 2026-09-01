"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";

import { FullPageLoader } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!isLoading && !user) {
      const redirect = pathname ? `?redirect=${encodeURIComponent(pathname)}` : "";
      router.replace(`/login${redirect}`);
    }
  }, [isLoading, pathname, router, user]);

  if (isLoading || !user) {
    return <FullPageLoader label="Đang kiểm tra phiên đăng nhập..." />;
  }

  return children;
}
