import {
  forwardRef,
  type ButtonHTMLAttributes,
  type ReactNode,
} from "react";

import { cn } from "@/lib/utils";

type ButtonVariant =
  | "primary"
  | "secondary"
  | "outline"
  | "ghost"
  | "danger";
type ButtonSize = "sm" | "md" | "lg";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: ReactNode;
}

const variantClasses: Record<ButtonVariant, string> = {
  primary:
    "text-on-accent shadow-sm [background-image:var(--gradient-action)] hover:brightness-110",
  secondary:
    "border border-line-subtle bg-surface-subtle text-content shadow-sm hover:bg-surface-tertiary",
  outline:
    "border border-line bg-surface text-content shadow-sm hover:border-line-strong hover:bg-surface-subtle",
  ghost:
    "bg-transparent text-content-secondary hover:bg-surface-subtle hover:text-content",
  danger:
    "bg-danger-action text-on-accent shadow-sm hover:bg-danger-action-hover",
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: "h-11 rounded-lg px-3 text-sm",
  md: "h-11 rounded-xl px-4 text-sm",
  lg: "h-12 rounded-xl px-5 text-base",
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant = "primary",
      size = "md",
      icon,
      children,
      type = "button",
      ...props
    },
    ref,
  ) => (
    <button
      ref={ref}
      type={type}
      className={cn(
        "inline-flex items-center justify-center gap-2 font-medium transition-[background-color,border-color,color,box-shadow,filter,opacity] duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface disabled:cursor-not-allowed disabled:opacity-60",
        variantClasses[variant],
        sizeClasses[size],
        className,
      )}
      {...props}
    >
      {icon}
      {children}
    </button>
  ),
);

Button.displayName = "Button";
