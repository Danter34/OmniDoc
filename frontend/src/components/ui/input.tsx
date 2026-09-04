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
        "h-11 w-full rounded-xl border bg-surface px-3.5 text-sm text-content outline-none transition-[background-color,border-color,box-shadow,color] placeholder:text-muted focus:border-focus-ring focus:ring-4 focus:ring-focus-glow disabled:cursor-not-allowed disabled:bg-surface-subtle disabled:text-muted",
        error
          ? "border-danger focus:border-danger focus:ring-danger-glow"
          : "border-line-subtle",
        className,
      )}
      {...props}
    />
  ),
);

Input.displayName = "Input";
