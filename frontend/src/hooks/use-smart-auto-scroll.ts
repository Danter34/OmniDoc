"use client";

import { useCallback, useEffect, useRef, useState } from "react";

const AUTO_SCROLL_THRESHOLD = 100;

export function useSmartAutoScroll(trigger: string | number) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [isPinnedToBottom, setIsPinnedToBottom] = useState(true);

  const scrollToBottom = useCallback((behavior: ScrollBehavior = "smooth") => {
    const container = containerRef.current;

    if (!container) {
      return;
    }

    setIsPinnedToBottom(true);
    container.scrollTo({
      top: container.scrollHeight,
      behavior,
    });
  }, []);

  const handleScroll = useCallback(() => {
    const container = containerRef.current;

    if (!container) {
      return;
    }

    const distanceFromBottom =
      container.scrollHeight - container.scrollTop - container.clientHeight;
    setIsPinnedToBottom(distanceFromBottom <= AUTO_SCROLL_THRESHOLD);
  }, []);

  useEffect(() => {
    if (!isPinnedToBottom) {
      return;
    }

    const animationFrame = window.requestAnimationFrame(() => {
      const container = containerRef.current;

      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    });

    return () => window.cancelAnimationFrame(animationFrame);
  }, [isPinnedToBottom, trigger]);

  return {
    containerRef,
    isPinnedToBottom,
    handleScroll,
    scrollToBottom,
  };
}
