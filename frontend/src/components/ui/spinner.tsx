import { LoaderCircle } from "lucide-react";

import { cn } from "@/lib/utils";

export function Spinner({ className }: { className?: string }) {
  return <LoaderCircle className={cn("size-4 animate-spin", className)} />;
}

export function FullPageLoader({ label }: { label: string }) {
  return (
    <div className="ambient-bg flex min-h-screen items-center justify-center">
      <div className="glass-panel flex items-center gap-3 rounded-2xl px-5 py-4 text-sm text-content-secondary">
        <Spinner className="size-5 text-accent" />
        {label}
      </div>
    </div>
  );
}
