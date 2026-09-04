"use client";

import { Check, Laptop, Moon, Sun, type LucideIcon } from "lucide-react";
import {
  useEffect,
  useId,
  useRef,
  useState,
  useSyncExternalStore,
  type KeyboardEvent,
} from "react";

import {
  useTheme,
  type ThemePreference,
} from "@/components/theme/theme-provider";
import { cn } from "@/lib/utils";

interface ThemeOption {
  value: ThemePreference;
  label: string;
  description: string;
  icon: LucideIcon;
}

const THEME_OPTIONS: ThemeOption[] = [
  {
    value: "light",
    label: "Sáng",
    description: "Luôn sử dụng giao diện sáng",
    icon: Sun,
  },
  {
    value: "dark",
    label: "Tối",
    description: "Luôn sử dụng giao diện tối",
    icon: Moon,
  },
  {
    value: "system",
    label: "Hệ thống",
    description: "Theo cài đặt của thiết bị",
    icon: Laptop,
  },
];

const subscribeToHydration = () => () => undefined;

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const groupId = useId();
  const hydrated = useSyncExternalStore(
    subscribeToHydration,
    () => true,
    () => false,
  );
  const visibleTheme = hydrated ? theme : "system";
  const selectedOption =
    THEME_OPTIONS.find((option) => option.value === visibleTheme) ??
    THEME_OPTIONS[2];
  const SelectedIcon = selectedOption.icon;

  useEffect(() => {
    if (!open) {
      return;
    }

    const selectedRadio = rootRef.current?.querySelector<HTMLButtonElement>(
      '[role="radio"][aria-checked="true"]',
    );
    selectedRadio?.focus();

    const handlePointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };

    document.addEventListener("pointerdown", handlePointerDown);
    return () => document.removeEventListener("pointerdown", handlePointerDown);
  }, [open]);

  const handleGroupKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const currentIndex = THEME_OPTIONS.findIndex(
      (option) => option.value === theme,
    );
    let nextIndex: number | null = null;

    if (event.key === "ArrowDown" || event.key === "ArrowRight") {
      nextIndex = (currentIndex + 1) % THEME_OPTIONS.length;
    } else if (event.key === "ArrowUp" || event.key === "ArrowLeft") {
      nextIndex =
        (currentIndex - 1 + THEME_OPTIONS.length) % THEME_OPTIONS.length;
    } else if (event.key === "Home") {
      nextIndex = 0;
    } else if (event.key === "End") {
      nextIndex = THEME_OPTIONS.length - 1;
    } else if (event.key === "Escape") {
      event.preventDefault();
      setOpen(false);
      triggerRef.current?.focus();
      return;
    }

    if (nextIndex === null) {
      return;
    }

    event.preventDefault();
    const nextOption = THEME_OPTIONS[nextIndex];
    setTheme(nextOption.value);
    rootRef.current
      ?.querySelector<HTMLButtonElement>(`[data-theme-option="${nextOption.value}"]`)
      ?.focus();
  };

  return (
    <div
      ref={rootRef}
      className="relative"
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) {
          setOpen(false);
        }
      }}
    >
      <button
        ref={triggerRef}
        aria-controls={groupId}
        aria-expanded={open}
        aria-label={`Chọn chủ đề. Hiện tại: ${selectedOption.label}`}
        className="flex size-11 items-center justify-center rounded-xl border border-line-subtle bg-surface/80 text-content-secondary shadow-sm transition-colors hover:bg-surface-subtle hover:text-content focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
        onClick={() => setOpen((current) => !current)}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown") {
            event.preventDefault();
            setOpen(true);
          }
        }}
        type="button"
      >
        <SelectedIcon aria-hidden="true" className="size-[18px]" />
      </button>

      {open ? (
        <div
          id={groupId}
          aria-label="Chọn chế độ giao diện"
          className="glass-panel absolute right-0 top-[calc(100%+8px)] z-40 w-64 rounded-2xl p-2"
          onKeyDown={handleGroupKeyDown}
          role="radiogroup"
        >
          {THEME_OPTIONS.map((option) => {
            const Icon = option.icon;
            const selected = theme === option.value;

            return (
              <button
                key={option.value}
                aria-checked={selected}
                className={cn(
                  "flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-inset",
                  selected
                    ? "bg-info-subtle text-content"
                    : "text-content-secondary hover:bg-surface-subtle hover:text-content",
                )}
                data-theme-option={option.value}
                onClick={() => {
                  setTheme(option.value);
                  setOpen(false);
                  triggerRef.current?.focus();
                }}
                role="radio"
                tabIndex={selected ? 0 : -1}
                type="button"
              >
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-surface-subtle text-accent">
                  <Icon aria-hidden="true" className="size-[18px]" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-medium">{option.label}</span>
                  <span className="mt-0.5 block text-xs text-muted">
                    {option.description}
                  </span>
                </span>
                <Check
                  aria-hidden="true"
                  className={cn(
                    "size-4 text-accent transition-opacity",
                    selected ? "opacity-100" : "opacity-0",
                  )}
                />
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
