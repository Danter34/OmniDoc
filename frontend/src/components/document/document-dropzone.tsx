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
    <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div
        aria-label="Tải tài liệu PDF"
        className={cn(
          "relative flex min-h-48 flex-col items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed px-6 py-8 text-center outline-none transition",
          isDragging
            ? "border-blue-500 bg-blue-50"
            : "border-slate-200 bg-slate-50/70 hover:border-blue-300 hover:bg-blue-50/40 focus-visible:border-blue-500 focus-visible:ring-4 focus-visible:ring-blue-500/10",
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
            <span className="flex size-12 items-center justify-center rounded-2xl bg-blue-100 text-blue-600">
              <Spinner className="size-6" />
            </span>
            <p className="mt-4 text-sm font-semibold text-slate-900">
              Đang tải tài liệu lên...
            </p>
            <p className="mt-1.5 text-xs text-slate-500">
              {completed}/{total} tệp hoàn tất
            </p>
            <div className="mt-5 h-2 w-full max-w-xs overflow-hidden rounded-full bg-slate-200">
              <div
                className="h-full rounded-full bg-blue-600 transition-[width] duration-300"
                style={{ width: `${uploadPercent}%` }}
              />
            </div>
          </>
        ) : (
          <>
            <span
              className={cn(
                "flex size-12 items-center justify-center rounded-2xl transition",
                isDragging
                  ? "bg-blue-600 text-white"
                  : "bg-blue-100 text-blue-600",
              )}
            >
              {isDragging ? (
                <FileUp className="size-6" />
              ) : (
                <UploadCloud className="size-6" />
              )}
            </span>
            <p className="mt-4 text-sm font-semibold text-slate-900">
              {isDragging
                ? "Thả PDF để tải lên"
                : "Kéo thả PDF vào đây hoặc nhấp để chọn"}
            </p>
            <p className="mt-1.5 text-xs leading-5 text-slate-500">
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
              ? "bg-emerald-50 text-emerald-700"
              : "bg-rose-50 text-rose-700",
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
