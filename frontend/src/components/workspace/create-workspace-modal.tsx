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
            className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700"
            role="alert"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            {error}
          </div>
        ) : null}

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-slate-700">
            Tên Workspace <span className="text-rose-500">*</span>
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
          <span className="mt-1.5 block text-right text-xs text-slate-400">
            {name.length}/256
          </span>
        </label>

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-slate-700">
            Mô tả
          </span>
          <textarea
            className="min-h-24 w-full resize-none rounded-xl border border-slate-200 bg-white px-3.5 py-3 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10 disabled:bg-slate-50"
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
