"use client";

import {
  AlertCircle,
  ArrowDownCircle,
  ArrowUpCircle,
  Check,
  Copy,
  LogOut,
  MailWarning,
  MoreHorizontal,
  ShieldCheck,
  Trash2,
  UserPlus,
  Users,
  X,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { useWorkspace } from "@/hooks/use-workspace";
import { cn, getInitials } from "@/lib/utils";
import { ApiError, getErrorMessage } from "@/services/api-client";
import { workspaceService } from "@/services/workspace.service";
import type {
  Workspace,
  WorkspaceInvitation,
  WorkspaceMember,
  WorkspaceRole,
} from "@/types/workspace.types";

interface RemovalTarget {
  member: WorkspaceMember;
  isSelfRemoval: boolean;
}

const joinedDateFormatter = new Intl.DateTimeFormat("vi-VN", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

export function WorkspaceMembersSettings({ workspace }: { workspace: Workspace }) {
  const router = useRouter();
  const { user, openVerificationModal } = useAuth();
  const { refreshWorkspaces } = useWorkspace();
  const [members, setMembers] = useState<WorkspaceMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [openMenuUserId, setOpenMenuUserId] = useState<string | null>(null);
  const [processingUserId, setProcessingUserId] = useState<string | null>(null);
  const [removalTarget, setRemovalTarget] = useState<RemovalTarget | null>(null);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState<WorkspaceRole>("Member");
  const [invitation, setInvitation] = useState<WorkspaceInvitation | null>(null);
  const [isInviting, setIsInviting] = useState(false);
  const [copied, setCopied] = useState(false);
  const copyTimerRef = useRef<number | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [showInviteVerificationGate, setShowInviteVerificationGate] =
    useState(false);
  const [forbiddenToast, setForbiddenToast] = useState<string | null>(null);

  const isOwner = workspace.role === "Owner";
  const isAdmin = workspace.role === "Admin";
  const canInviteMembers = isOwner || isAdmin;
  const ownerCount = useMemo(
    () => members.filter((member) => member.role === "Owner").length,
    [members],
  );

  useEffect(() => {
    const controller = new AbortController();

    workspaceService
      .getMembers(workspace.id, controller.signal)
      .then((items) => {
        setMembers(items);
        setError(null);
      })
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
  }, [workspace.id]);

  useEffect(() => {
    if (!forbiddenToast) return;

    const timer = window.setTimeout(() => setForbiddenToast(null), 5000);
    return () => window.clearTimeout(timer);
  }, [forbiddenToast]);

  useEffect(() => {
    if (!openMenuUserId) return;

    const menu = document.getElementById(`member-menu-${openMenuUserId}`);
    const focusFrame = window.requestAnimationFrame(() => {
      menu
        ?.querySelector<HTMLButtonElement>('[role="menuitem"]:not([disabled])')
        ?.focus();
    });

    function handlePointerDown(event: PointerEvent) {
      if (
        !(event.target instanceof Element) ||
        !event.target.closest("[data-member-menu-root]")
      ) {
        setOpenMenuUserId(null);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        document.getElementById(`member-menu-trigger-${openMenuUserId}`)?.focus();
        setOpenMenuUserId(null);
        return;
      }

      if (
        event.key !== "ArrowDown" &&
        event.key !== "ArrowUp" &&
        event.key !== "Home" &&
        event.key !== "End"
      ) {
        return;
      }

      const items = Array.from(
        menu?.querySelectorAll<HTMLButtonElement>(
          '[role="menuitem"]:not([disabled])',
        ) ?? [],
      );
      if (items.length === 0) return;

      event.preventDefault();
      const currentIndex = items.findIndex(
        (item) => item === document.activeElement,
      );
      const nextIndex =
        event.key === "Home"
          ? 0
          : event.key === "End"
            ? items.length - 1
            : event.key === "ArrowUp"
              ? (currentIndex - 1 + items.length) % items.length
              : (currentIndex + 1) % items.length;
      items[nextIndex]?.focus();
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [openMenuUserId]);

  useEffect(
    () => () => {
      if (copyTimerRef.current !== null) {
        window.clearTimeout(copyTimerRef.current);
      }
    },
    [],
  );

  function showForbiddenError(requestError: unknown) {
    if (requestError instanceof ApiError && requestError.status === 403) {
      setForbiddenToast(
        getErrorMessage(requestError) ||
          "Bạn không có quyền thực hiện thao tác này trong Workspace.",
      );
    }
  }

  async function handleRoleChange(
    member: WorkspaceMember,
    newRole: WorkspaceRole,
  ) {
    setOpenMenuUserId(null);
    setProcessingUserId(member.userId);
    setError(null);

    try {
      const updated = await workspaceService.updateMemberRole(
        workspace.id,
        member.userId,
        newRole,
      );
      setMembers((current) =>
        current.map((item) => item.userId === updated.userId ? updated : item),
      );
      await refreshWorkspaces();
    } catch (requestError) {
      showForbiddenError(requestError);
      setError(getErrorMessage(requestError));
    } finally {
      setProcessingUserId(null);
    }
  }

  async function handleRemoveMember() {
    if (!removalTarget) {
      return;
    }

    const { member, isSelfRemoval } = removalTarget;
    setProcessingUserId(member.userId);
    setError(null);

    try {
      await workspaceService.removeMember(workspace.id, member.userId);
      setRemovalTarget(null);
      await refreshWorkspaces();

      if (isSelfRemoval) {
        router.replace("/workspaces");
        return;
      }

      setMembers((current) =>
        current.filter((item) => item.userId !== member.userId),
      );
    } catch (requestError) {
      showForbiddenError(requestError);
      setError(getErrorMessage(requestError));
      setRemovalTarget(null);
    } finally {
      setProcessingUserId(null);
    }
  }

  async function handleInvite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsInviting(true);
    setInviteError(null);

    try {
      const created = await workspaceService.inviteMember(workspace.id, {
        email: inviteEmail.trim(),
        role: inviteRole,
      });
      setInvitation(created);
    } catch (requestError) {
      if (
        requestError instanceof ApiError &&
        requestError.errorCode === "EMAIL_NOT_VERIFIED"
      ) {
        closeInviteModal();
        setShowInviteVerificationGate(true);
        return;
      }
      showForbiddenError(requestError);
      setInviteError(getErrorMessage(requestError));
    } finally {
      setIsInviting(false);
    }
  }

  function handleOpenInvite() {
    if (!canInviteMembers) return;

    if (!user?.emailConfirmed) {
      setShowInviteVerificationGate(true);
      return;
    }

    setShowInviteVerificationGate(false);
    setInviteRole("Member");
    setInviteOpen(true);
  }

  function handleOpenVerification() {
    setShowInviteVerificationGate(false);
    openVerificationModal();
  }

  function closeInviteModal() {
    if (copyTimerRef.current !== null) {
      window.clearTimeout(copyTimerRef.current);
      copyTimerRef.current = null;
    }
    setInviteOpen(false);
    setInviteEmail("");
    setInviteRole("Member");
    setInvitation(null);
    setCopied(false);
    setInviteError(null);
  }

  async function copyInviteLink() {
    if (!invitation) {
      return;
    }

    try {
      await navigator.clipboard.writeText(invitation.inviteLink);
      setCopied(true);
      setInviteError(null);
      if (copyTimerRef.current !== null) {
        window.clearTimeout(copyTimerRef.current);
      }
      copyTimerRef.current = window.setTimeout(() => {
        copyTimerRef.current = null;
        setCopied(false);
      }, 2000);
    } catch {
      setInviteError("Không thể sao chép tự động. Vui lòng chọn và sao chép liên kết.");
    }
  }

  return (
    <section className="glass-panel overflow-hidden rounded-2xl">
      <header className="flex flex-col gap-4 border-b border-line-subtle px-5 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <div>
          <div className="flex items-center gap-2 text-sm font-medium text-accent">
            <ShieldCheck className="size-4" />
            Cài đặt workspace
          </div>
          <h1 className="mt-1.5 text-xl font-semibold text-content">
            Thành viên & Quyền hạn
          </h1>
          <p className="mt-1 text-sm text-muted">
            Quản lý người có quyền truy cập vào {workspace.name}.
          </p>
        </div>
        {canInviteMembers ? (
          <Button icon={<UserPlus className="size-4" />} onClick={handleOpenInvite}>
            Mời thành viên
          </Button>
        ) : null}
      </header>

      {error ? (
        <div className="mx-5 mt-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger sm:mx-6" role="alert">
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
          <span>{error}</span>
        </div>
      ) : null}

      <div className="px-5 py-5 sm:px-6">
        <div className="mb-4 flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm font-medium text-content-secondary">
            <Users className="size-4 text-muted" />
            {members.length} thành viên
          </div>
          {!isOwner ? (
            <span className="text-xs text-muted">
              Bạn có quyền {workspace.role}
            </span>
          ) : null}
        </div>

        {isLoading ? (
          <div className="flex min-h-52 items-center justify-center gap-3 text-sm text-muted">
            <Spinner className="size-5 text-accent" />
            Đang tải danh sách thành viên...
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line-subtle bg-surface/55">
            <table className="w-full min-w-[720px] border-collapse text-left">
              <thead className="bg-surface-subtle text-xs font-semibold uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3">Thành viên</th>
                  <th className="px-4 py-3">Vai trò</th>
                  <th className="px-4 py-3">Ngày tham gia</th>
                  <th className="w-16 px-4 py-3"><span className="sr-only">Hành động</span></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line-subtle">
                {members.map((member) => {
                  const isCurrentUser = member.userId === user?.id;
                  const protectsLastOwner =
                    member.role === "Owner" && ownerCount === 1;
                  const isProcessing = processingUserId === member.userId;

                  return (
                    <tr className="text-sm" key={member.userId}>
                      <td className="px-4 py-3.5">
                        <div className="flex items-center gap-3">
                          <div className="flex size-9 shrink-0 items-center justify-center rounded-full bg-info-subtle text-xs font-semibold text-accent">
                            {getInitials(member.fullName)}
                          </div>
                          <div className="min-w-0">
                            <p className="truncate font-medium text-content">
                              {member.fullName}
                              {isCurrentUser ? <span className="ml-1.5 text-xs font-normal text-muted">(Bạn)</span> : null}
                            </p>
                            <p className="mt-0.5 truncate text-xs text-muted">{member.email}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={cn(
                          "inline-flex rounded-full px-2.5 py-1 text-xs font-semibold",
                          member.role === "Owner"
                            ? "bg-role-owner-subtle text-role-owner ring-1 ring-inset ring-role-owner-line"
                            : member.role === "Admin"
                              ? "bg-role-admin-subtle text-role-admin ring-1 ring-inset ring-role-admin-line"
                              : "bg-role-member-subtle text-role-member ring-1 ring-inset ring-role-member-line",
                        )}>
                          {member.role}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 text-muted">
                        {joinedDateFormatter.format(new Date(member.joinedAt))}
                      </td>
                      <td className="relative px-4 py-3.5 text-right" data-member-menu-root>
                        {isProcessing ? (
                          <Spinner className="ml-auto size-4 text-accent" />
                        ) : isOwner || (isAdmin && member.role === "Member") ? (
                          <button
                            aria-label={`Thao tác với ${member.fullName}`}
                            aria-expanded={openMenuUserId === member.userId}
                            aria-haspopup="menu"
                            aria-controls={`member-menu-${member.userId}`}
                            className="inline-flex size-11 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                            id={`member-menu-trigger-${member.userId}`}
                            onClick={() => setOpenMenuUserId((current) => current === member.userId ? null : member.userId)}
                            type="button"
                          >
                            <MoreHorizontal className="size-4" />
                          </button>
                        ) : null}

                        {openMenuUserId === member.userId ? (
                          <div
                            aria-labelledby={`member-menu-trigger-${member.userId}`}
                            className="glass-panel absolute right-4 top-14 z-20 w-56 rounded-xl p-1.5 text-left"
                            id={`member-menu-${member.userId}`}
                            role="menu"
                          >
                            {isOwner && member.role === "Member" ? (
                              <button
                                className="flex min-h-11 w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-content-secondary transition-colors hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                                onClick={() => void handleRoleChange(member, "Admin")}
                                role="menuitem"
                                type="button"
                              >
                                <ArrowUpCircle className="size-4" />
                                Thăng cấp lên Admin
                              </button>
                            ) : null}
                            {isOwner && member.role === "Admin" ? (
                              <button
                                className="flex min-h-11 w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-content-secondary transition-colors hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                                onClick={() => void handleRoleChange(member, "Member")}
                                role="menuitem"
                                type="button"
                              >
                                <ArrowDownCircle className="size-4" />
                                Chuyển thành Member
                              </button>
                            ) : null}
                            {isOwner && member.role === "Owner" ? (
                              <button
                                className="flex min-h-11 w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-content-secondary transition-colors hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring disabled:cursor-not-allowed disabled:opacity-50"
                                disabled={protectsLastOwner}
                                onClick={() => void handleRoleChange(member, "Member")}
                                role="menuitem"
                                type="button"
                              >
                                <ArrowDownCircle className="size-4" />
                                Chuyển thành Member
                              </button>
                            ) : null}
                            {isOwner && member.role !== "Owner" ? (
                              <button
                                className="flex min-h-11 w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-content-secondary transition-colors hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
                                onClick={() => void handleRoleChange(member, "Owner")}
                                role="menuitem"
                                type="button"
                              >
                                <ArrowUpCircle className="size-4" />
                                Nhượng quyền Owner
                              </button>
                            ) : null}
                            <button
                              className="flex min-h-11 w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-danger transition-colors hover:bg-danger-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring disabled:cursor-not-allowed disabled:opacity-50"
                              disabled={protectsLastOwner}
                              onClick={() => {
                                setOpenMenuUserId(null);
                                setRemovalTarget({ member, isSelfRemoval: isCurrentUser });
                              }}
                              role="menuitem"
                              type="button"
                            >
                              <Trash2 className="size-4" />
                              {isCurrentUser ? "Rời workspace" : "Xóa khỏi Workspace"}
                            </button>
                          </div>
                        ) : null}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {!isOwner && user ? (
          <div className="mt-5 flex flex-col gap-3 rounded-xl border border-line-subtle bg-surface-subtle px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-sm font-medium text-content">Rời workspace</p>
              <p className="mt-0.5 text-xs text-muted">Bạn sẽ mất quyền truy cập vào tài liệu và cuộc trò chuyện.</p>
            </div>
            <Button
              icon={<LogOut className="size-4" />}
              onClick={() => {
                const self = members.find((member) => member.userId === user.id);
                if (self) setRemovalTarget({ member: self, isSelfRemoval: true });
              }}
              variant="secondary"
            >
              Rời workspace
            </Button>
          </div>
        ) : null}
      </div>

      <Modal
        description="Xác minh email giúp bảo vệ Workspace và hạn chế lời mời không mong muốn."
        onClose={() => setShowInviteVerificationGate(false)}
        open={showInviteVerificationGate && !Boolean(user?.emailConfirmed)}
        title="Cần xác minh email"
      >
        <div className="flex items-start gap-3 rounded-xl border border-warning bg-warning-subtle px-4 py-3.5 text-sm leading-6 text-warning">
          <MailWarning className="mt-1 size-5 shrink-0" />
          <p>
            Tài khoản của bạn cần được xác minh email trước khi gửi lời mời
            vào Workspace.
          </p>
        </div>
        <div className="mt-6 flex justify-end gap-2">
          <Button
            onClick={() => setShowInviteVerificationGate(false)}
            variant="secondary"
          >
            Để sau
          </Button>
          <Button onClick={handleOpenVerification}>Xác minh ngay</Button>
        </div>
      </Modal>

      <Modal
        description="Chọn email và vai trò khởi tạo. Liên kết sẽ hết hạn sau 7 ngày."
        onClose={closeInviteModal}
        open={inviteOpen}
        title="Mời thành viên mới"
      >
        {inviteError ? (
          <div className="mb-4 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger" role="alert">
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            <span>{inviteError}</span>
          </div>
        ) : null}
        {invitation ? (
          <div>
            <div className="rounded-xl border border-success bg-success-subtle px-4 py-3 text-sm text-success">
              Đã tạo lời mời cho <strong>{invitation.inviteeEmail}</strong> với vai trò {invitation.role}.
            </div>
            <label className="mt-5 block">
              <span className="mb-1.5 block text-sm font-medium text-content-secondary">Liên kết mời</span>
              <div className="flex gap-2">
                <Input readOnly value={invitation.inviteLink} />
                <Button
                  className="shrink-0"
                  icon={copied ? <Check className="size-4" /> : <Copy className="size-4" />}
                  onClick={() => void copyInviteLink()}
                  variant="secondary"
                >
                  {copied ? "Đã chép" : "Copy Link"}
                </Button>
              </div>
            </label>
            <div className="mt-5 flex justify-end gap-2">
              <Button onClick={closeInviteModal} variant="secondary">Đóng</Button>
              <Button onClick={() => {
                setInvitation(null);
                setInviteEmail("");
                setCopied(false);
                setInviteError(null);
              }}>Tạo lời mời khác</Button>
            </div>
          </div>
        ) : (
          <form className="space-y-4" onSubmit={handleInvite}>
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-content-secondary">Email</span>
              <Input
                autoFocus
                onChange={(event) => setInviteEmail(event.target.value)}
                placeholder="colleague@company.com"
                required
                type="email"
                value={inviteEmail}
              />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-content-secondary">Vai trò khởi tạo</span>
              <select
                className="h-11 w-full rounded-xl border border-line-subtle bg-surface px-3.5 text-sm text-content outline-none transition focus:border-focus-ring focus:ring-4 focus:ring-focus-glow"
                disabled={isAdmin}
                onChange={(event) => setInviteRole(event.target.value as WorkspaceRole)}
                value={inviteRole}
              >
                <option value="Member">Member — xem và cộng tác</option>
                {isOwner ? (
                  <>
                    <option value="Admin">Admin — quản trị thành viên</option>
                    <option value="Owner">Owner — toàn quyền quản trị</option>
                  </>
                ) : null}
              </select>
            </label>
            <div className="flex justify-end gap-2 pt-2">
              <Button onClick={closeInviteModal} variant="secondary">Hủy</Button>
              <Button disabled={isInviting || !inviteEmail.trim()} type="submit">
                {isInviting ? <Spinner /> : <UserPlus className="size-4" />}
                {isInviting ? "Đang tạo..." : "Tạo lời mời"}
              </Button>
            </div>
          </form>
        )}
      </Modal>

      <Modal
        description={removalTarget?.isSelfRemoval
          ? "Bạn sẽ không thể truy cập workspace này sau khi rời đi."
          : "Thành viên sẽ mất quyền truy cập vào tài liệu và cuộc trò chuyện của workspace."}
        onClose={() => setRemovalTarget(null)}
        open={Boolean(removalTarget)}
        title={removalTarget?.isSelfRemoval ? "Rời workspace?" : "Xóa thành viên?"}
      >
        <p className="text-sm text-content-secondary">
          {removalTarget?.isSelfRemoval
            ? `Bạn có chắc muốn rời ${workspace.name}?`
            : `Bạn có chắc muốn xóa ${removalTarget?.member.fullName ?? "thành viên này"} khỏi workspace?`}
        </p>
        <div className="mt-6 flex justify-end gap-2">
          <Button onClick={() => setRemovalTarget(null)} variant="secondary">Hủy</Button>
          <Button
            disabled={Boolean(processingUserId)}
            onClick={() => void handleRemoveMember()}
            variant="danger"
          >
            {processingUserId ? <Spinner /> : null}
            {removalTarget?.isSelfRemoval ? "Rời workspace" : "Xóa thành viên"}
          </Button>
        </div>
      </Modal>

      {forbiddenToast ? (
        <div
          className="glass-panel fixed bottom-5 right-5 z-[80] flex w-[min(380px,calc(100vw-40px))] items-start gap-3 rounded-2xl border-warning p-4 text-sm text-content-secondary"
          role="alert"
        >
          <AlertCircle className="mt-0.5 size-5 shrink-0 text-warning" />
          <div className="min-w-0 flex-1">
            <p className="font-semibold text-content">Không đủ quyền truy cập</p>
            <p className="mt-1 leading-5">{forbiddenToast}</p>
          </div>
          <button
            aria-label="Đóng thông báo"
            className="flex size-11 shrink-0 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
            onClick={() => setForbiddenToast(null)}
            type="button"
          >
            <X className="size-4" />
          </button>
        </div>
      ) : null}
    </section>
  );
}
