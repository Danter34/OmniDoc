"use client";

import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Clock3,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

import { Button } from "@/components/ui/button";
import { Logo } from "@/components/ui/logo";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import { invitationService } from "@/services/invitation.service";
import type { InvitationDetails } from "@/types/workspace.types";

const expiryFormatter = new Intl.DateTimeFormat("vi-VN", {
  dateStyle: "long",
  timeStyle: "short",
});

export function InvitationAcceptancePage({ token }: { token: string }) {
  const router = useRouter();
  const { user, isLoading: isAuthLoading } = useAuth();
  const [invitation, setInvitation] = useState<InvitationDetails | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isAccepting, setIsAccepting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    invitationService
      .getDetails(token, controller.signal)
      .then(setInvitation)
      .catch((requestError: unknown) => {
        if (
          requestError instanceof DOMException &&
          requestError.name === "AbortError"
        ) {
          return;
        }
        setError(getErrorMessage(requestError));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [token]);

  async function acceptInvitation() {
    setIsAccepting(true);
    setError(null);

    try {
      const accepted = await invitationService.accept(token);
      router.replace(`/workspaces/${accepted.workspaceId}/chat`);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      setIsAccepting(false);
    }
  }

  const returnPath = `/invitations/${encodeURIComponent(token)}`;
  const loginHref = `/login?redirect=${encodeURIComponent(returnPath)}`;
  const registerHref = `/register?redirect=${encodeURIComponent(returnPath)}`;
  const canAccept = invitation?.status === "Pending";

  return (
    <main className="ambient-bg relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-10">
      <section className="glass-panel relative w-full max-w-lg rounded-3xl p-7 sm:p-9">
        <Logo className="justify-center" />

        {isLoading ? (
          <div className="flex min-h-72 flex-col items-center justify-center gap-3 text-sm text-muted">
            <Spinner className="size-6 text-accent" />
            Đang kiểm tra lời mời...
          </div>
        ) : invitation ? (
          <div className="mt-8 text-center">
            <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-info-subtle text-accent">
              <ShieldCheck className="size-7" />
            </div>
            <p className="mt-5 text-sm font-medium text-accent">Lời mời cộng tác</p>
            <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-content">
              Tham gia {invitation.workspaceName}
            </h1>
            <p className="mt-3 text-sm leading-6 text-muted">
              <strong className="font-medium text-content-secondary">{invitation.inviterName}</strong>{" "}
              đã mời bạn tham gia workspace với vai trò{" "}
              <strong
                className={cn(
                  "font-semibold",
                  invitation.role === "Owner"
                    ? "text-role-owner"
                    : invitation.role === "Admin"
                      ? "text-role-admin"
                      : "text-role-member",
                )}
              >
                {invitation.role}
              </strong>.
            </p>

            <div className="mt-6 grid gap-3 rounded-2xl border border-line-subtle bg-surface-subtle p-4 text-left sm:grid-cols-2">
              <div className="flex items-start gap-2.5">
                <UserRound className="mt-0.5 size-4 text-muted" />
                <div>
                  <p className="text-xs text-muted">Vai trò</p>
                  <p
                    className={cn(
                      "mt-1 inline-flex rounded-full px-2 py-0.5 text-sm font-semibold ring-1 ring-inset",
                      invitation.role === "Owner"
                        ? "bg-role-owner-subtle text-role-owner ring-role-owner-line"
                        : invitation.role === "Admin"
                          ? "bg-role-admin-subtle text-role-admin ring-role-admin-line"
                          : "bg-role-member-subtle text-role-member ring-role-member-line",
                    )}
                  >
                    {invitation.role}
                  </p>
                </div>
              </div>
              <div className="flex items-start gap-2.5">
                <Clock3 className="mt-0.5 size-4 text-muted" />
                <div>
                  <p className="text-xs text-muted">Hết hạn</p>
                  <p className="mt-0.5 text-sm font-medium text-content">
                    {expiryFormatter.format(new Date(invitation.expiresAt))}
                  </p>
                </div>
              </div>
            </div>

            {invitation.status !== "Pending" ? (
              <div className="mt-5 flex items-center justify-center gap-2 rounded-xl border border-warning bg-warning-subtle px-4 py-3 text-sm text-warning">
                <AlertCircle className="size-4" />
                {invitation.status === "Accepted"
                  ? "Lời mời này đã được sử dụng."
                  : invitation.status === "Expired"
                    ? "Lời mời này đã hết hạn."
                    : "Lời mời này đã bị thu hồi."}
              </div>
            ) : null}

            {error ? (
              <div className="mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-left text-sm text-danger" role="alert">
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                <span>{error}</span>
              </div>
            ) : null}

            <div className="mt-6">
              {!canAccept ? (
                <Button className="w-full" disabled size="lg">
                  Lời mời không còn hiệu lực
                </Button>
              ) : isAuthLoading ? (
                <Button className="w-full" disabled size="lg">
                  <Spinner /> Đang kiểm tra phiên đăng nhập...
                </Button>
              ) : user ? (
                <Button
                  className="w-full"
                  disabled={!canAccept || isAccepting}
                  onClick={() => void acceptInvitation()}
                  size="lg"
                >
                  {isAccepting ? <Spinner /> : <CheckCircle2 className="size-5" />}
                  {isAccepting ? "Đang tham gia..." : "Tham gia Workspace"}
                </Button>
              ) : (
                <div className="space-y-3">
                  <Link
                    className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl px-5 text-base font-medium text-on-accent shadow-sm transition-[filter,box-shadow] hover:brightness-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 [background-image:var(--gradient-action)]"
                    href={loginHref}
                  >
                    Đăng nhập để tham gia
                    <ArrowRight className="size-4" />
                  </Link>
                  <p className="text-xs text-muted">
                    Chưa có tài khoản?{" "}
                    <Link className="font-medium text-accent hover:text-accent-primary" href={registerHref}>
                      Đăng ký miễn phí
                    </Link>
                  </p>
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="mt-8 flex min-h-72 flex-col items-center justify-center text-center">
            <div className="flex size-14 items-center justify-center rounded-2xl bg-danger-subtle text-danger">
              <AlertCircle className="size-7" />
            </div>
            <h1 className="mt-5 text-xl font-semibold text-content">Không thể mở lời mời</h1>
            <p className="mt-2 max-w-sm text-sm leading-6 text-muted">
              {error ?? "Liên kết lời mời không hợp lệ hoặc không còn tồn tại."}
            </p>
            <Link className="mt-6 text-sm font-medium text-accent hover:text-accent-primary" href="/">
              Quay về OmniDoc
            </Link>
          </div>
        )}
      </section>
    </main>
  );
}
