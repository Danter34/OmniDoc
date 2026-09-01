"use client";

import { Send, Square, WandSparkles } from "lucide-react";
import {
  useEffect,
  useRef,
  type FormEvent,
  type KeyboardEvent,
} from "react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface ChatInputProps {
  value: string;
  disabled: boolean;
  isStreaming: boolean;
  disabledReason?: string;
  onChange: (value: string) => void;
  onSend: () => void;
  onStop: () => void;
}

export function ChatInput({
  value,
  disabled,
  isStreaming,
  disabledReason,
  onChange,
  onSend,
  onStop,
}: ChatInputProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    const textarea = textareaRef.current;

    if (!textarea) {
      return;
    }

    textarea.style.height = "0px";
    textarea.style.height = `${Math.min(textarea.scrollHeight, 180)}px`;
  }, [value]);

  function submit(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();

    if (!disabled && !isStreaming && value.trim()) {
      onSend();
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  }

  return (
    <form
      className="border-t border-slate-200 bg-white p-3 sm:p-4"
      onSubmit={submit}
    >
      <div
        className={cn(
          "mx-auto max-w-4xl rounded-2xl border bg-white p-2 shadow-sm transition focus-within:border-blue-400 focus-within:ring-4 focus-within:ring-blue-500/10",
          disabled ? "border-slate-200 bg-slate-50" : "border-slate-300",
        )}
      >
        <textarea
          aria-label="Nhập câu hỏi"
          className="block min-h-11 max-h-45 w-full resize-none bg-transparent px-2.5 py-2 text-sm leading-6 text-slate-900 outline-none placeholder:text-slate-400 disabled:cursor-not-allowed disabled:text-slate-400"
          disabled={disabled || isStreaming}
          maxLength={4000}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={
            disabled
              ? "Cần ít nhất một tài liệu đã lập chỉ mục..."
              : "Hỏi OmniDoc về tài liệu trong Workspace..."
          }
          ref={textareaRef}
          rows={1}
          value={value}
        />
        <div className="flex items-center justify-between gap-3 px-1 pt-1">
          <p className="flex min-w-0 items-center gap-1.5 truncate text-[11px] text-slate-400">
            <WandSparkles className="size-3.5 shrink-0" />
            {disabledReason || "Enter để gửi · Shift+Enter để xuống dòng"}
          </p>
          {isStreaming ? (
            <Button
              icon={<Square className="size-3.5 fill-current" />}
              onClick={onStop}
              size="sm"
              variant="secondary"
            >
              Dừng sinh
            </Button>
          ) : (
            <Button
              aria-label="Gửi câu hỏi"
              className="size-9 shrink-0 px-0"
              disabled={disabled || !value.trim()}
              type="submit"
            >
              <Send className="size-4" />
            </Button>
          )}
        </div>
      </div>
    </form>
  );
}
