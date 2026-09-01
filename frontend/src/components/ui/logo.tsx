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
      <span className="flex size-9 items-center justify-center rounded-xl bg-blue-600 text-white shadow-sm shadow-blue-600/20">
        <FileStack className="size-5" strokeWidth={2.2} />
      </span>
      {!compact ? (
        <span className="text-lg font-semibold tracking-tight text-slate-950">
          Omni<span className="text-blue-600">Doc</span>
        </span>
      ) : null}
    </div>
  );
}
