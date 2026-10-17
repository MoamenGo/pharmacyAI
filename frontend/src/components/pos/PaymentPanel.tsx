"use client";

import { forwardRef } from "react";
import { formatPiasters } from "@/lib/format";

interface Props {
  totalPiasters: number;
  itemCount: number;
  paidText: string;
  /** Null while the box is empty: the customer pays exactly the total. */
  paidPiasters: number | null;
  onPaidText: (text: string) => void;
  blockedReason: string | null;
  saving: boolean;
  error: string | null;
  onSubmit: () => void;
}

export const PaymentPanel = forwardRef<HTMLInputElement, Props>(function PaymentPanel(
  { totalPiasters, itemCount, paidText, paidPiasters, onPaidText, blockedReason, saving, error, onSubmit },
  paidRef,
) {
  const paid = paidPiasters ?? totalPiasters;
  const change = paid - totalPiasters;
  const invalidPaid = paidText.trim() !== "" && paidPiasters === null;

  return (
    <aside className="space-y-4 rounded-xl border border-slate-800 bg-slate-900 p-5">
      <div>
        <div className="text-sm text-slate-400">الإجمالي ({itemCount} صنف)</div>
        <div className="font-mono text-4xl font-bold text-emerald-400" dir="ltr" data-testid="sale-total">
          {formatPiasters(totalPiasters)}
        </div>
      </div>

      <label className="block">
        <span className="mb-1 block text-sm text-slate-400">المدفوع (فاضي = المبلغ بالظبط)</span>
        <input
          ref={paidRef}
          value={paidText}
          onChange={(e) => onPaidText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              onSubmit();
            }
          }}
          inputMode="decimal"
          placeholder={formatPiasters(totalPiasters)}
          className={`w-full rounded-lg border bg-slate-950 px-3 py-2 font-mono text-2xl ${invalidPaid ? "border-rose-600" : "border-slate-700"}`}
          aria-label="المبلغ المدفوع"
          autoComplete="off"
          dir="ltr"
        />
      </label>

      <div className="flex items-baseline justify-between">
        <span className="text-slate-400">الباقي</span>
        <span className={`font-mono text-2xl ${change < 0 ? "text-rose-400" : "text-amber-300"}`} dir="ltr" data-testid="sale-change">
          {formatPiasters(change)}
        </span>
      </div>

      {(error ?? blockedReason) && (
        <p role="alert" className="text-rose-400">
          {error ?? blockedReason}
        </p>
      )}

      <button
        type="button"
        onClick={onSubmit}
        disabled={saving || blockedReason !== null}
        className="w-full rounded-xl bg-emerald-600 py-3 text-lg font-bold text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
      >
        {saving ? "جاري الحفظ…" : "حفظ وطباعة (F10)"}
      </button>
    </aside>
  );
});
