"use client";

import { AlertCircle } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { useWorkspace } from "@/hooks/use-workspace";
import { getErrorMessage } from "@/services/api-client";

interface CreateWorkspaceModalProps {
  open: boolean;
  onClose: () => void;
}

export function CreateWorkspaceModal({
  open,
  onClose,
}: CreateWorkspaceModalProps) {
  const router = useRouter();
  const { createWorkspace } = useWorkspace();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function resetAndClose() {
    setName("");
    setDescription("");
    setError(null);
    setIsSubmitting(false);
    onClose();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedName = name.trim();

    if (!normalizedName) {
      setError("Vui lòng nhập tên workspace.");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const workspace = await createWorkspace({
        name: normalizedName,
        description: description.trim() || undefined,
      });
      resetAndClose();
      router.push(`/workspaces/${workspace.id}`);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      setIsSubmitting(false);
    }
  }

  return (
    <Modal
      description="Tập hợp tài liệu và cộng tác trong một không gian riêng."
      onClose={resetAndClose}
      open={open}
      title="Tạo Workspace mới"
    >
      <form className="space-y-4" onSubmit={handleSubmit}>
        {error ? (
          <div
            className="flex items-start gap-2 rounded-xl border border-danger bg-danger-subtle p-3 text-sm text-danger"
            role="alert"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            {error}
          </div>
        ) : null}

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-content-secondary">
            Tên Workspace <span className="text-danger">*</span>
          </span>
          <Input
            autoFocus
            disabled={isSubmitting}
            maxLength={256}
            onChange={(event) => {
              setName(event.target.value);
              setError(null);
            }}
            placeholder="Ví dụ: Phòng Pháp chế"
            value={name}
          />
          <span className="mt-1.5 block text-right text-xs text-muted">
            {name.length}/256
          </span>
        </label>

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-content-secondary">
            Mô tả
          </span>
          <textarea
            className="min-h-24 w-full resize-none rounded-xl border border-line-subtle bg-surface px-3.5 py-3 text-sm text-content outline-none transition placeholder:text-muted focus:border-focus-ring focus:ring-4 focus:ring-focus-glow disabled:bg-surface-subtle"
            disabled={isSubmitting}
            maxLength={1000}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Mô tả ngắn về mục đích của workspace..."
            value={description}
          />
        </label>

        <div className="flex justify-end gap-3 pt-2">
          <Button
            disabled={isSubmitting}
            onClick={resetAndClose}
            variant="secondary"
          >
            Hủy
          </Button>
          <Button disabled={isSubmitting} type="submit">
            {isSubmitting ? <Spinner /> : null}
            {isSubmitting ? "Đang tạo..." : "Tạo Workspace"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
