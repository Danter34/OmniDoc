import { FileStack } from "lucide-react";

import { cn } from "@/lib/utils";

export function Logo({
  compact = false,
  className,
}: {
  compact?: boolean;
  className?: string;
}) {
  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      <span className="flex size-9 items-center justify-center rounded-xl text-brand-icon shadow-[0_0_18px_var(--brand-icon-shadow)] [background-image:var(--gradient-brand)]">
        <FileStack className="size-5" strokeWidth={2.2} />
      </span>
      {!compact ? (
        <span className="text-lg font-semibold tracking-tight text-content">
          Omni<span className="text-accent">Doc</span>
        </span>
      ) : null}
    </div>
  );
}
