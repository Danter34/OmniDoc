"use client";

import { Send, Square, WandSparkles } from "lucide-react";
import {
  memo,
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

function ChatInputComponent({
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
      className="shrink-0 bg-transparent px-3 pb-3 pt-2 sm:px-5 sm:pb-4"
      onSubmit={submit}
    >
      <div
        className={cn(
          "glass-panel mx-auto max-w-4xl rounded-[1.4rem] p-2 transition-[background-color,border-color,box-shadow] focus-within:border-focus-ring focus-within:shadow-[var(--accent-glow)]",
          disabled && "bg-surface-subtle opacity-80",
        )}
      >
        <textarea
          aria-label="Nhập câu hỏi"
          className="block min-h-11 max-h-45 w-full resize-none bg-transparent px-2.5 py-2 text-sm leading-6 text-content outline-none placeholder:text-muted disabled:cursor-not-allowed disabled:text-muted"
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
          <p className="flex min-w-0 items-center gap-1.5 truncate text-[11px] text-muted">
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
              className="group size-11 shrink-0 rounded-full px-0 shadow-[var(--accent-glow)]"
              disabled={disabled || !value.trim()}
              type="submit"
            >
              <Send className="size-4 transition-transform group-active:-translate-y-0.5 group-active:rotate-12" />
            </Button>
          )}
        </div>
      </div>
    </form>
  );
}

export const ChatInput = memo(ChatInputComponent);
