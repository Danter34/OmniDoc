import Image from "next/image";
import Link from "next/link";

import { cn } from "@/lib/utils";

export function Logo({
  compact = false,
  className,
  href,
  imageSize = 48,
  priority = false,
}: {
  compact?: boolean;
  className?: string;
  href?: string;
  imageSize?: 32 | 48;
  priority?: boolean;
}) {
  const content = (
    <>
      <Image
        alt="Biểu tượng OmniDoc"
        className="shrink-0 rounded-full border border-line-subtle shadow-[0_0_18px_var(--brand-icon-shadow)]"
        height={imageSize}
        priority={priority}
        src="/images/logo-icon.png"
        width={imageSize}
      />
      {!compact ? (
        <span className="font-semibold text-lg tracking-tight text-content">
          Omni<span className="text-accent">Doc</span>
        </span>
      ) : null}
    </>
  );

  if (href) {
    return (
      <Link
        aria-label="OmniDoc — về danh sách workspace"
        className={cn(
          "flex items-center gap-2.5 rounded-xl transition-[opacity,filter] hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring focus-visible:ring-offset-2 focus-visible:ring-offset-surface",
          className,
        )}
        href={href}
      >
        {content}
      </Link>
    );
  }

  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      {content}
    </div>
  );
}
