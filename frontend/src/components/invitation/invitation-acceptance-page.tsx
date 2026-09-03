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
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-50 px-4 py-10">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -left-32 top-16 size-80 rounded-full bg-blue-100/70 blur-3xl" />
        <div className="absolute -right-28 bottom-10 size-72 rounded-full bg-cyan-100/60 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(to_right,#e2e8f044_1px,transparent_1px),linear-gradient(to_bottom,#e2e8f044_1px,transparent_1px)] bg-[size:32px_32px]" />
      </div>

      <section className="relative w-full max-w-lg rounded-3xl border border-slate-200 bg-white p-7 shadow-2xl shadow-slate-950/[0.08] sm:p-9">
        <Logo className="justify-center" />

        {isLoading ? (
          <div className="flex min-h-72 flex-col items-center justify-center gap-3 text-sm text-slate-500">
            <Spinner className="size-6 text-blue-600" />
            Đang kiểm tra lời mời...
          </div>
        ) : invitation ? (
          <div className="mt-8 text-center">
            <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-blue-50 text-blue-600">
              <ShieldCheck className="size-7" />
            </div>
            <p className="mt-5 text-sm font-medium text-blue-600">Lời mời cộng tác</p>
            <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-slate-950">
              Tham gia {invitation.workspaceName}
            </h1>
            <p className="mt-3 text-sm leading-6 text-slate-500">
              <strong className="font-medium text-slate-700">{invitation.inviterName}</strong>{" "}
              đã mời bạn tham gia workspace với vai trò{" "}
              <strong className="font-medium text-slate-700">{invitation.role}</strong>.
            </p>

            <div className="mt-6 grid gap-3 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-left sm:grid-cols-2">
              <div className="flex items-start gap-2.5">
                <UserRound className="mt-0.5 size-4 text-slate-400" />
                <div>
                  <p className="text-xs text-slate-500">Vai trò</p>
                  <p className="mt-0.5 text-sm font-medium text-slate-800">{invitation.role}</p>
                </div>
              </div>
              <div className="flex items-start gap-2.5">
                <Clock3 className="mt-0.5 size-4 text-slate-400" />
                <div>
                  <p className="text-xs text-slate-500">Hết hạn</p>
                  <p className="mt-0.5 text-sm font-medium text-slate-800">
                    {expiryFormatter.format(new Date(invitation.expiresAt))}
                  </p>
                </div>
              </div>
            </div>

            {invitation.status !== "Pending" ? (
              <div className="mt-5 flex items-center justify-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
                <AlertCircle className="size-4" />
                {invitation.status === "Accepted"
                  ? "Lời mời này đã được sử dụng."
                  : invitation.status === "Expired"
                    ? "Lời mời này đã hết hạn."
                    : "Lời mời này đã bị thu hồi."}
              </div>
            ) : null}

            {error ? (
              <div className="mt-5 flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-left text-sm text-rose-700" role="alert">
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
                    className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 text-base font-medium text-white shadow-sm transition hover:bg-blue-700"
                    href={loginHref}
                  >
                    Đăng nhập để tham gia
                    <ArrowRight className="size-4" />
                  </Link>
                  <p className="text-xs text-slate-500">
                    Chưa có tài khoản?{" "}
                    <Link className="font-medium text-blue-600 hover:text-blue-700" href={registerHref}>
                      Đăng ký miễn phí
                    </Link>
                  </p>
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="mt-8 flex min-h-72 flex-col items-center justify-center text-center">
            <div className="flex size-14 items-center justify-center rounded-2xl bg-rose-50 text-rose-600">
              <AlertCircle className="size-7" />
            </div>
            <h1 className="mt-5 text-xl font-semibold text-slate-950">Không thể mở lời mời</h1>
            <p className="mt-2 max-w-sm text-sm leading-6 text-slate-500">
              {error ?? "Liên kết lời mời không hợp lệ hoặc không còn tồn tại."}
            </p>
            <Link className="mt-6 text-sm font-medium text-blue-600 hover:text-blue-700" href="/">
              Quay về OmniDoc
            </Link>
          </div>
        )}
      </section>
    </main>
  );
}
