"use client";

import {
  AlertCircle,
  CheckCircle2,
  Eye,
  EyeOff,
  KeyRound,
  LockKeyhole,
} from "lucide-react";
import { useEffect, useRef, useState, type FormEvent } from "react";

import { PasswordStrength } from "@/components/auth/password-strength";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/hooks/use-auth";
import { getErrorMessage } from "@/services/api-client";

export function ChangePasswordModal({ onClose }: { onClose: () => void }) {
  const { changePassword } = useAuth();
  const closeTimerRef = useRef<number | null>(null);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [visible, setVisible] = useState({ current: false, next: false, confirm: false });
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isComplete, setIsComplete] = useState(false);

  useEffect(
    () => () => {
      if (closeTimerRef.current !== null) {
        window.clearTimeout(closeTimerRef.current);
      }
    },
    [],
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (newPassword.length < 8) {
      setError("Mật khẩu mới cần có ít nhất 8 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await changePassword({ currentPassword, newPassword });
      setIsComplete(true);
      closeTimerRef.current = window.setTimeout(onClose, 1400);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setIsSubmitting(false);
    }
  }

  function toggleVisibility(field: keyof typeof visible) {
    setVisible((current) => ({ ...current, [field]: !current[field] }));
  }

  return (
    <Modal
      description={
        isComplete
          ? "Phiên hiện tại đã nhận JWT mới; các phiên cũ đã bị thu hồi."
          : "Sau khi đổi, OmniDoc sẽ thu hồi các phiên đăng nhập cũ và giữ phiên hiện tại hoạt động an toàn."
      }
      onClose={onClose}
      open
      title={isComplete ? "Đổi mật khẩu thành công" : "Đổi mật khẩu"}
    >
      {isComplete ? (
        <div className="py-4 text-center" role="status">
          <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-success-subtle text-success">
            <CheckCircle2 className="size-8" />
          </div>
          <p className="mt-4 text-sm font-medium text-success">
            Mật khẩu và phiên đăng nhập đã được cập nhật.
          </p>
        </div>
      ) : (
        <>
          {error ? (
            <div
              className="mb-5 flex items-start gap-2.5 rounded-xl border border-danger bg-danger-subtle px-4 py-3 text-sm text-danger"
              role="alert"
            >
              <AlertCircle className="mt-0.5 size-4 shrink-0" />
              <span>{error}</span>
            </div>
          ) : null}

          <form className="space-y-4" onSubmit={handleSubmit}>
            <PasswordField
              autoComplete="current-password"
              autoFocus
              icon={<LockKeyhole className="size-4" />}
              label="Mật khẩu hiện tại"
              onChange={(value) => {
                setCurrentPassword(value);
                setError(null);
              }}
              onToggle={() => toggleVisibility("current")}
              show={visible.current}
              value={currentPassword}
            />

            <div>
              <PasswordField
                autoComplete="new-password"
                icon={<KeyRound className="size-4" />}
                label="Mật khẩu mới"
                onChange={(value) => {
                  setNewPassword(value);
                  setError(null);
                }}
                onToggle={() => toggleVisibility("next")}
                show={visible.next}
                value={newPassword}
              />
              <PasswordStrength password={newPassword} />
            </div>

            <PasswordField
              autoComplete="new-password"
              error={Boolean(confirmPassword && confirmPassword !== newPassword)}
              icon={<KeyRound className="size-4" />}
              label="Xác nhận mật khẩu mới"
              onChange={(value) => {
                setConfirmPassword(value);
                setError(null);
              }}
              onToggle={() => toggleVisibility("confirm")}
              show={visible.confirm}
              value={confirmPassword}
            />

            <div className="flex justify-end gap-2 pt-2">
              <Button onClick={onClose} variant="secondary">
                Hủy
              </Button>
              <Button
                disabled={
                  isSubmitting ||
                  !currentPassword ||
                  !newPassword ||
                  !confirmPassword
                }
                type="submit"
              >
                {isSubmitting ? <Spinner /> : <KeyRound className="size-4" />}
                {isSubmitting ? "Đang cập nhật..." : "Đổi mật khẩu"}
              </Button>
            </div>
          </form>
        </>
      )}
    </Modal>
  );
}

function PasswordField({
  autoComplete,
  autoFocus = false,
  error = false,
  icon,
  label,
  onChange,
  onToggle,
  show,
  value,
}: {
  autoComplete: string;
  autoFocus?: boolean;
  error?: boolean;
  icon: React.ReactNode;
  label: string;
  onChange: (value: string) => void;
  onToggle: () => void;
  show: boolean;
  value: string;
}) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-content-secondary">
        {label}
      </span>
      <div className="relative">
        <span className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted">
          {icon}
        </span>
        <Input
          autoComplete={autoComplete}
          autoFocus={autoFocus}
          className="pl-10 pr-12"
          error={error}
          maxLength={128}
          onChange={(event) => onChange(event.target.value)}
          required
          type={show ? "text" : "password"}
          value={value}
        />
        <button
          aria-label={show ? `Ẩn ${label.toLowerCase()}` : `Hiện ${label.toLowerCase()}`}
          className="absolute right-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content-secondary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
          onClick={onToggle}
          type="button"
        >
          {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
        </button>
      </div>
    </label>
  );
}
