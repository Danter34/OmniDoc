"use client";

import { Info, X } from "lucide-react";
import { useState } from "react";

export function ShowcaseBanner() {
  const [collapsed, setCollapsed] = useState(false);
  return (
    <aside aria-label="Thông báo không gian trải nghiệm" className="border-b border-info/30 bg-info-subtle px-4 py-2 text-info">
      {collapsed ? (
        <button type="button" onClick={() => setCollapsed(false)} aria-expanded={false} className="flex min-h-9 items-center gap-2 rounded-lg text-xs font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring">
          <Info aria-hidden="true" className="size-4" /> Không gian dùng chung · Xem thông báo
        </button>
      ) : (
        <div className="flex items-start gap-2">
          <Info aria-hidden="true" className="mt-2 size-4 shrink-0" />
          <p className="flex-1 py-1 text-xs leading-6">Không gian trải nghiệm công khai dùng chung. Vui lòng không nhập thông tin nhạy cảm hoặc bí mật cá nhân.</p>
          <button type="button" aria-label="Thu gọn thông báo không gian dùng chung" aria-expanded={true} onClick={() => setCollapsed(true)} className="flex size-9 shrink-0 items-center justify-center rounded-lg hover:bg-surface-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus-ring">
            <X aria-hidden="true" className="size-4" />
          </button>
        </div>
      )}
    </aside>
  );
}
