"use client";

import { useCallback, useEffect, useState } from "react";

export const SHOWCASE_GUARD_MESSAGE =
  "Thao tác này không khả dụng ở tài khoản thử nghiệm.";

let activeGuardMessage: string | null = null;
const subscribers = new Set<(msg: string | null) => void>();
let autoDismissTimer: number | null = null;

export function fireShowcaseGuard(message = SHOWCASE_GUARD_MESSAGE) {
  activeGuardMessage = message;
  if (autoDismissTimer !== null) {
    window.clearTimeout(autoDismissTimer);
  }
  autoDismissTimer = window.setTimeout(() => {
    activeGuardMessage = null;
    autoDismissTimer = null;
    subscribers.forEach((notify) => notify(null));
  }, 5000);

  subscribers.forEach((notify) => notify(message));
}

export function clearShowcaseGuard() {
  activeGuardMessage = null;
  if (autoDismissTimer !== null) {
    window.clearTimeout(autoDismissTimer);
    autoDismissTimer = null;
  }
  subscribers.forEach((notify) => notify(null));
}

/** Shared toast state for intercepting mutations in showcase mode. */
export function useShowcaseGuard() {
  const [guardMessage, setGuardMessage] = useState<string | null>(activeGuardMessage);

  useEffect(() => {
    const handler = (msg: string | null) => setGuardMessage(msg);
    subscribers.add(handler);
    return () => {
      subscribers.delete(handler);
    };
  }, []);

  const fireGuard = useCallback(() => {
    fireShowcaseGuard(SHOWCASE_GUARD_MESSAGE);
  }, []);

  const clearGuard = useCallback(() => {
    clearShowcaseGuard();
  }, []);

  return { guardMessage, fireGuard, clearGuard };
}
