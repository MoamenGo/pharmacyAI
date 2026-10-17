"use client";

import { useEffect, useRef } from "react";

export type FunctionKey = "F1" | "F2" | "F3" | "F4" | "F5" | "F6" | "F7" | "F8" | "F9" | "F10" | "F11" | "F12";

export type FunctionKeyMap = Partial<Record<FunctionKey, () => void>>;

/**
 * Binds F1–F12 for the current screen. Bound keys never reach the webview,
 * so F5 cannot reload the app in the middle of a sale.
 */
export function useFunctionKeys(bindings: FunctionKeyMap) {
  const ref = useRef(bindings);
  ref.current = bindings;

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const handler = ref.current[event.key as FunctionKey];
      if (!handler) return;
      event.preventDefault();
      handler();
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
}
