"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export const SHOWCASE_GUARD_MESSAGE =
  "Thao tác này không khả dụng ở tài khoản thử nghiệm.";

/** Shared toast state for intercepting mutations in showcase mode. */
export function useShowcaseGuard() {
  const [guardMessage, setGuardMessage] = useState<string | null>(null);
  const timerRef = useRef<number | null>(null);

  // Auto-dismiss after 5 s, matching the existing forbiddenToast pattern.
  useEffect(() => {
    if (!guardMessage) return;
    timerRef.current = window.setTimeout(() => setGuardMessage(null), 5000);
    return () => {
      if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    };
  }, [guardMessage]);

  // Cleanup on unmount.
  useEffect(
    () => () => {
      if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    },
    [],
  );

  const fireGuard = useCallback(() => {
    setGuardMessage(SHOWCASE_GUARD_MESSAGE);
  }, []);

  const clearGuard = useCallback(() => {
    setGuardMessage(null);
  }, []);

  return { guardMessage, fireGuard, clearGuard };
}
