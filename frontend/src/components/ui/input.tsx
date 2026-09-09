import { forwardRef, type InputHTMLAttributes } from "react";

import { cn } from "@/lib/utils";

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  error?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, error, ...props }, ref) => (
    <input
      ref={ref}
      aria-invalid={error || undefined}
      className={cn(
        "h-10 w-full rounded-lg border bg-white dark:bg-slate-950 px-3 text-sm text-content outline-none transition-[background-color,border-color,box-shadow,color] placeholder:text-muted focus:ring-2 disabled:cursor-not-allowed disabled:bg-surface-subtle disabled:text-muted",
        error
          ? "border-danger focus:border-danger focus:ring-danger-glow"
          : "border-slate-300 dark:border-slate-700 focus:border-blue-500 focus:ring-blue-500",
        className,
      )}
      {...props}
    />
  ),
);

Input.displayName = "Input";
