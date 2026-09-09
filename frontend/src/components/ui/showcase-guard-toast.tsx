import { AlertCircle, X } from "lucide-react";

/**
 * Fixed bottom-right toast for showcase guard intercepts.
 * Style matches the existing forbiddenToast in workspace-members-settings.tsx.
 */
export function ShowcaseGuardToast({
  message,
  onDismiss,
}: {
  message: string;
  onDismiss: () => void;
}) {
  return (
    <div
      className="glass-panel fixed bottom-5 right-5 z-[80] flex w-[min(380px,calc(100vw-40px))] items-start gap-3 rounded-2xl border-warning p-4 text-sm text-content-secondary"
      role="alert"
    >
      <AlertCircle className="mt-0.5 size-5 shrink-0 text-warning" />
      <div className="min-w-0 flex-1">
        <p className="font-semibold text-content">Tài khoản thử nghiệm</p>
        <p className="mt-1 leading-5">{message}</p>
      </div>
      <button
        aria-label="Đóng thông báo"
        className="flex size-11 shrink-0 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring"
        onClick={onDismiss}
        type="button"
      >
        <X className="size-4" />
      </button>
    </div>
  );
}
