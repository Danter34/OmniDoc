"use client";

import {
  AlertCircle,
  ArrowDownCircle,
  ArrowUpCircle,
  Check,
  Copy,
  LogOut,
  MoreHorizontal,
  ShieldCheck,
  Trash2,
  UserPlus,
  Users,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { useWorkspace } from "@/hooks/use-workspace";
import { cn, getInitials } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
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
  const { user } = useAuth();
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
  const [inviteError, setInviteError] = useState<string | null>(null);

  const isOwner = workspace.role === "Owner";
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

  async function handleRoleChange(member: WorkspaceMember) {
    const newRole: WorkspaceRole = member.role === "Owner" ? "Member" : "Owner";
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
      setInviteError(getErrorMessage(requestError));
    } finally {
      setIsInviting(false);
    }
  }

  function closeInviteModal() {
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
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setInviteError("Không thể sao chép tự động. Vui lòng chọn và sao chép liên kết.");
    }
  }

  return (
    <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
      <header className="flex flex-col gap-4 border-b border-slate-200 px-5 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <div>
          <div className="flex items-center gap-2 text-sm font-medium text-blue-600">
            <ShieldCheck className="size-4" />
            Cài đặt workspace
          </div>
          <h1 className="mt-1.5 text-xl font-semibold text-slate-950">
            Thành viên & Quyền hạn
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Quản lý người có quyền truy cập vào {workspace.name}.
          </p>
        </div>
        {isOwner ? (
          <Button icon={<UserPlus className="size-4" />} onClick={() => setInviteOpen(true)}>
            Mời thành viên
          </Button>
        ) : null}
      </header>

      {error ? (
        <div className="mx-5 mt-5 flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700 sm:mx-6" role="alert">
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
          <span>{error}</span>
        </div>
      ) : null}

      <div className="px-5 py-5 sm:px-6">
        <div className="mb-4 flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm font-medium text-slate-700">
            <Users className="size-4 text-slate-400" />
            {members.length} thành viên
          </div>
          {!isOwner ? (
            <span className="text-xs text-slate-500">Bạn có quyền Member</span>
          ) : null}
        </div>

        {isLoading ? (
          <div className="flex min-h-52 items-center justify-center gap-3 text-sm text-slate-500">
            <Spinner className="size-5 text-blue-600" />
            Đang tải danh sách thành viên...
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-slate-200">
            <table className="w-full min-w-[720px] border-collapse text-left">
              <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Thành viên</th>
                  <th className="px-4 py-3">Vai trò</th>
                  <th className="px-4 py-3">Ngày tham gia</th>
                  <th className="w-16 px-4 py-3"><span className="sr-only">Hành động</span></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {members.map((member) => {
                  const isCurrentUser = member.userId === user?.id;
                  const protectsLastOwner =
                    member.role === "Owner" && ownerCount === 1;
                  const isProcessing = processingUserId === member.userId;

                  return (
                    <tr className="text-sm" key={member.userId}>
                      <td className="px-4 py-3.5">
                        <div className="flex items-center gap-3">
                          <div className="flex size-9 shrink-0 items-center justify-center rounded-full bg-blue-50 text-xs font-semibold text-blue-700">
                            {getInitials(member.fullName)}
                          </div>
                          <div className="min-w-0">
                            <p className="truncate font-medium text-slate-900">
                              {member.fullName}
                              {isCurrentUser ? <span className="ml-1.5 text-xs font-normal text-slate-400">(Bạn)</span> : null}
                            </p>
                            <p className="mt-0.5 truncate text-xs text-slate-500">{member.email}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={cn(
                          "inline-flex rounded-full px-2.5 py-1 text-xs font-semibold",
                          member.role === "Owner"
                            ? "bg-blue-50 text-blue-700 ring-1 ring-inset ring-blue-200"
                            : "bg-slate-100 text-slate-600 ring-1 ring-inset ring-slate-200",
                        )}>
                          {member.role}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 text-slate-500">
                        {joinedDateFormatter.format(new Date(member.joinedAt))}
                      </td>
                      <td className="relative px-4 py-3.5 text-right">
                        {isProcessing ? (
                          <Spinner className="ml-auto size-4 text-blue-600" />
                        ) : isOwner ? (
                          <button
                            aria-label={`Thao tác với ${member.fullName}`}
                            className="inline-flex size-8 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
                            onClick={() => setOpenMenuUserId((current) => current === member.userId ? null : member.userId)}
                            type="button"
                          >
                            <MoreHorizontal className="size-4" />
                          </button>
                        ) : null}

                        {openMenuUserId === member.userId ? (
                          <div className="absolute right-4 top-12 z-20 w-56 rounded-xl border border-slate-200 bg-white p-1.5 text-left shadow-xl shadow-slate-950/10">
                            <button
                              className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                              disabled={protectsLastOwner}
                              onClick={() => void handleRoleChange(member)}
                              type="button"
                            >
                              {member.role === "Owner" ? <ArrowDownCircle className="size-4" /> : <ArrowUpCircle className="size-4" />}
                              {member.role === "Owner" ? "Đổi thành Member" : "Thăng cấp lên Owner"}
                            </button>
                            <button
                              className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-rose-600 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-50"
                              disabled={protectsLastOwner}
                              onClick={() => {
                                setOpenMenuUserId(null);
                                setRemovalTarget({ member, isSelfRemoval: isCurrentUser });
                              }}
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
          <div className="mt-5 flex flex-col gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-sm font-medium text-slate-800">Rời workspace</p>
              <p className="mt-0.5 text-xs text-slate-500">Bạn sẽ mất quyền truy cập vào tài liệu và cuộc trò chuyện.</p>
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
        description="Chọn email và vai trò khởi tạo. Liên kết sẽ hết hạn sau 7 ngày."
        onClose={closeInviteModal}
        open={inviteOpen}
        title="Mời thành viên mới"
      >
        {inviteError ? (
          <div className="mb-4 flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700" role="alert">
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            <span>{inviteError}</span>
          </div>
        ) : null}
        {invitation ? (
          <div>
            <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
              Đã tạo lời mời cho <strong>{invitation.inviteeEmail}</strong> với vai trò {invitation.role}.
            </div>
            <label className="mt-5 block">
              <span className="mb-1.5 block text-sm font-medium text-slate-700">Liên kết mời</span>
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
              <span className="mb-1.5 block text-sm font-medium text-slate-700">Email</span>
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
              <span className="mb-1.5 block text-sm font-medium text-slate-700">Vai trò khởi tạo</span>
              <select
                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-sm text-slate-900 outline-none transition focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                onChange={(event) => setInviteRole(event.target.value as WorkspaceRole)}
                value={inviteRole}
              >
                <option value="Member">Member — xem và cộng tác</option>
                <option value="Owner">Owner — toàn quyền quản trị</option>
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
        <p className="text-sm text-slate-600">
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
    </section>
  );
}
