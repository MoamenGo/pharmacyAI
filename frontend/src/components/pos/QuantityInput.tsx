"use client";

import { forwardRef, useEffect, useState } from "react";
import { MAX_LINE_QUANTITY } from "@/stores/cartStore";

interface Props {
  value: number;
  label: string;
  onFocus: () => void;
  onChange: (quantity: number) => void;
  onEnter: () => void;
}

/**
 * Keeps its own text so the cashier can clear the box and type a new number;
 * the cart only ever sees whole quantities from 1 up, and an empty box snaps back on blur.
 */
export const QuantityInput = forwardRef<HTMLInputElement, Props>(function QuantityInput({ value, label, onFocus, onChange, onEnter }, ref) {
  const [text, setText] = useState(String(value));

  useEffect(() => setText(String(value)), [value]);

  const commit = (raw: string) => {
    const quantity = Number(raw);
    if (/^\d+$/.test(raw) && quantity >= 1 && quantity <= MAX_LINE_QUANTITY) onChange(quantity);
  };

  return (
    <input
      ref={ref}
      value={text}
      inputMode="numeric"
      onFocus={(e) => {
        onFocus();
        e.target.select();
      }}
      onChange={(e) => {
        setText(e.target.value);
        commit(e.target.value);
      }}
      onBlur={() => setText(String(value))}
      onKeyDown={(e) => {
        if (e.key === "Enter") {
          setText(String(value));
          onEnter();
        }
      }}
      className="w-20 rounded-lg border border-slate-700 bg-slate-950 px-2 py-1 font-mono"
      aria-label={label}
      dir="ltr"
    />
  );
});
