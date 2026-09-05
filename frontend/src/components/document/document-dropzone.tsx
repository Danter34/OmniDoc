"use client";

import {
  AlertCircle,
  CheckCircle2,
  FileUp,
  UploadCloud,
} from "lucide-react";
import {
  useRef,
  useState,
  type ChangeEvent,
  type DragEvent,
  type KeyboardEvent,
} from "react";

import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/api-client";
import type { WorkspaceDocument } from "@/types/document.types";

interface DocumentDropzoneProps {
  onUpload: (file: File) => Promise<WorkspaceDocument>;
}

function isPdf(file: File) {
  return (
    file.type === "application/pdf" ||
    file.name.toLowerCase().endsWith(".pdf")
  );
}

export function DocumentDropzone({ onUpload }: DocumentDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [completed, setCompleted] = useState(0);
  const [total, setTotal] = useState(0);
  const [message, setMessage] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);

  async function uploadFiles(files: File[]) {
    if (isUploading || files.length === 0) {
      return;
    }

    const pdfFiles = files.filter(isPdf);

    if (pdfFiles.length !== files.length) {
      setMessage({
        type: "error",
        text: "OmniDoc chỉ hỗ trợ tệp PDF. Các tệp không hợp lệ đã được bỏ qua.",
      });
    }

    if (pdfFiles.length === 0) {
      return;
    }

    setIsUploading(true);
    setCompleted(0);
    setTotal(pdfFiles.length);
    let successCount = 0;

    try {
      for (const file of pdfFiles) {
        try {
          await onUpload(file);
          successCount += 1;
        } catch (error) {
          setMessage({
            type: "error",
            text: `Không thể tải “${file.name}”: ${getErrorMessage(error)}`,
          });
        } finally {
          setCompleted((current) => current + 1);
        }
      }

      if (successCount === pdfFiles.length) {
        setMessage({
          type: "success",
          text:
            successCount === 1
              ? "Đã tải PDF lên. OmniDoc đang bắt đầu xử lý."
              : `Đã tải ${successCount} PDF lên. OmniDoc đang bắt đầu xử lý.`,
        });
      }
    } finally {
      setIsUploading(false);

      if (inputRef.current) {
        inputRef.current.value = "";
      }
    }
  }

  function handleInputChange(event: ChangeEvent<HTMLInputElement>) {
    void uploadFiles(Array.from(event.target.files ?? []));
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    void uploadFiles(Array.from(event.dataTransfer.files));
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if ((event.key === "Enter" || event.key === " ") && !isUploading) {
      event.preventDefault();
      inputRef.current?.click();
    }
  }

  const uploadPercent = total > 0 ? Math.round((completed / total) * 100) : 0;

  return (
    <section className="glass-panel rounded-2xl p-4 sm:p-5">
      <div
        aria-disabled={isUploading}
        aria-label="Tải tài liệu PDF"
        className={cn(
          "relative flex min-h-48 flex-col items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed px-6 py-8 text-center outline-none transition",
          isDragging
            ? "border-focus-ring bg-info-subtle shadow-[var(--accent-glow)]"
            : "border-line bg-surface-subtle/70 hover:border-line-strong hover:bg-info-subtle/60 focus-visible:border-focus-ring focus-visible:ring-4 focus-visible:ring-focus-glow",
          isUploading && "pointer-events-none",
        )}
        onDragEnter={(event) => {
          event.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={(event) => {
          event.preventDefault();

          if (!event.currentTarget.contains(event.relatedTarget as Node)) {
            setIsDragging(false);
          }
        }}
        onDragOver={(event) => event.preventDefault()}
        onDrop={handleDrop}
        onKeyDown={handleKeyDown}
        onClick={() => {
          if (!isUploading) {
            inputRef.current?.click();
          }
        }}
        role="button"
        tabIndex={0}
      >
        <input
          accept=".pdf,application/pdf"
          className="sr-only"
          disabled={isUploading}
          multiple
          onChange={handleInputChange}
          ref={inputRef}
          type="file"
        />

        {isUploading ? (
          <>
            <span className="flex size-12 items-center justify-center rounded-2xl bg-info-subtle text-accent">
              <Spinner className="size-6" />
            </span>
            <p className="mt-4 text-sm font-semibold text-content">
              Đang tải tài liệu lên...
            </p>
            <p className="mt-1.5 text-xs text-muted">
              {completed}/{total} tệp hoàn tất
            </p>
            <div
              aria-label="Tiến độ tải tài liệu"
              aria-valuemax={100}
              aria-valuemin={0}
              aria-valuenow={uploadPercent}
              className="mt-5 h-2 w-full max-w-xs overflow-hidden rounded-full bg-surface-tertiary"
              role="progressbar"
            >
              <div
                className="progress-shimmer relative h-full origin-left rounded-full transition-transform duration-300 [background-image:var(--gradient-progress)]"
                style={{ transform: `scaleX(${uploadPercent / 100})` }}
              />
            </div>
          </>
        ) : (
          <>
            <span
              className={cn(
                "flex size-12 items-center justify-center rounded-2xl transition",
                isDragging
                  ? "text-brand-icon shadow-[var(--accent-glow)] [background-image:var(--gradient-action)]"
                  : "bg-info-subtle text-accent",
              )}
            >
              {isDragging ? (
                <FileUp className="size-6" />
              ) : (
                <UploadCloud className="size-6" />
              )}
            </span>
            <p className="mt-4 text-sm font-semibold text-content">
              {isDragging
                ? "Thả PDF để tải lên"
                : "Kéo thả PDF vào đây hoặc nhấp để chọn"}
            </p>
            <p className="mt-1.5 text-xs leading-5 text-muted">
              Có thể chọn nhiều tệp PDF. Quá trình lập chỉ mục chạy nền.
            </p>
          </>
        )}
      </div>

      {message ? (
        <div
          className={cn(
            "mt-3 flex items-start gap-2 rounded-xl px-3 py-2.5 text-xs",
            message.type === "success"
              ? "bg-success-subtle text-success"
              : "bg-danger-subtle text-danger",
          )}
          role={message.type === "error" ? "alert" : "status"}
        >
          {message.type === "success" ? (
            <CheckCircle2 className="mt-0.5 size-3.5 shrink-0" />
          ) : (
            <AlertCircle className="mt-0.5 size-3.5 shrink-0" />
          )}
          {message.text}
        </div>
      ) : null}
    </section>
  );
}
