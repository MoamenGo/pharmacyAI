"use client";

import { forwardRef, useEffect, useState } from "react";
import { PAYMENT_METHODS } from "@/components/pos/PaymentPanel";
import { api, type Sale, type SaleReturn } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatCairoDateTime, formatPiasters } from "@/lib/format";

interface Props {
  sale: Sale;
  /** Called after a return is saved, with the sale as it is now. */
  onReturned: (sale: Sale, saved: SaleReturn) => void;
}

export const methodLabel = (method: string) => PAYMENT_METHODS.find((m) => m.value === method)?.label ?? method;

/** One receipt's lines, what already came back, and the form to take more back. */
export const SaleDetails = forwardRef<HTMLButtonElement, Props>(function SaleDetails({ sale, onReturned }, submitRef) {
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [reason, setReason] = useState("");
  const [returns, setReturns] = useState<SaleReturn[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setQuantities({});
    setReason("");
    setError(null);
    api.listReturnsForSale(sale.id).then(setReturns, (e) => setError(errorMessage(e)));
  }, [sale]);

  const items = sale.lines
    .map((l) => ({ line: l, quantity: Number(quantities[l.id] || 0) }))
    .filter((i) => i.quantity > 0);
  const invalid = items.some((i) => !Number.isInteger(i.quantity) || i.quantity > i.line.quantity - i.line.returnedQuantity);
  const fullyReturned = sale.lines.every((l) => l.returnedQuantity === l.quantity);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (items.length === 0 || invalid || saving) return;
    setSaving(true);
    setError(null);
    try {
      const saved = await api.createReturn(
        sale.id,
        items.map((i) => ({ saleLineId: i.line.id, quantity: i.quantity })),
        reason.trim() || null,
      );
      onReturned(await api.getSale(sale.id), saved);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-4 rounded-xl border border-slate-800 bg-slate-900 p-5" aria-label={`فاتورة رقم ${sale.number}`}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-lg font-bold">فاتورة رقم {sale.number}</h2>
        <span className="text-sm text-slate-400">
          <span dir="ltr">{formatCairoDateTime(sale.soldAtUtc)}</span> · {methodLabel(sale.paymentMethod)}
          {sale.customerName && ` · ${sale.customerName}`}
        </span>
      </div>

      <table className="w-full text-sm">
        <thead className="text-slate-400">
          <tr>
            <th className="py-1 text-right font-normal">الصنف</th>
            <th className="py-1 text-right font-normal">الكمية</th>
            <th className="py-1 text-right font-normal">الصافي</th>
            <th className="py-1 text-right font-normal">اترجع</th>
            <th className="py-1 text-right font-normal">يترجع دلوقتي</th>
          </tr>
        </thead>
        <tbody>
          {sale.lines.map((l) => {
            const left = l.quantity - l.returnedQuantity;
            return (
              <tr key={l.id} className="border-t border-slate-800">
                <td className="py-2">{l.drugNameAr}</td>
                <td className="py-2">
                  {l.quantity} {l.unitName}
                </td>
                <td className="py-2 font-mono" dir="ltr">
                  {formatPiasters(l.lineTotalPiasters - l.discountPiasters)}
                </td>
                <td className="py-2">{l.returnedQuantity > 0 ? `${l.returnedQuantity} ${l.unitName}` : "—"}</td>
                <td className="py-2">
                  {left > 0 ? (
                    <input
                      type="number"
                      min={0}
                      max={left}
                      value={quantities[l.id] ?? ""}
                      onChange={(e) => setQuantities((q) => ({ ...q, [l.id]: e.target.value }))}
                      aria-label={`كمية ترجيع ${l.drugNameAr}`}
                      placeholder={`لحد ${left}`}
                      className="w-24 rounded-lg border border-slate-700 bg-slate-950 px-2 py-1 focus:border-emerald-500 focus:outline-none"
                    />
                  ) : (
                    <span className="text-slate-500">اترجع كله</span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      <div className="text-sm text-slate-300">
        الإجمالي <span dir="ltr" className="font-mono">{formatPiasters(sale.totalPiasters)}</span>
        {sale.discountPiasters > 0 && (
          <>
            {" "}
            (بعد خصم <span dir="ltr" className="font-mono">{formatPiasters(sale.discountPiasters)}</span>)
          </>
        )}
      </div>

      {!fullyReturned && (
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex-1 space-y-1">
            <span className="block text-sm text-slate-400">السبب (اختياري)</span>
            <input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={200}
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 focus:border-emerald-500 focus:outline-none"
            />
          </label>
          <button
            ref={submitRef}
            type="submit"
            disabled={items.length === 0 || invalid || saving}
            className="rounded-lg bg-amber-600 px-5 py-2 font-semibold hover:bg-amber-500 disabled:opacity-50"
          >
            ترجيع (F9)
          </button>
        </div>
      )}
      {invalid && <p role="alert" className="text-sm text-rose-400">الكمية لازم تكون رقم صحيح ومش أكبر من اللي لسه ممكن يترجع</p>}
      {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}

      {returns.length > 0 && (
        <section className="space-y-1 text-sm" data-testid="sale-returns">
          <h3 className="font-semibold text-slate-300">مرتجعات الفاتورة دي</h3>
          {returns.map((r) => (
            <p key={r.id} className="text-slate-400">
              مرتجع {r.number} · <span dir="ltr">{formatCairoDateTime(r.returnedAtUtc)}</span> ·{" "}
              {r.lines.map((l) => `${l.quantity} ${l.unitName} ${l.drugNameAr}`).join("، ")} ·{" "}
              <span dir="ltr" className="font-mono">{formatPiasters(r.totalPiasters)}</span>
              {r.reason && ` · ${r.reason}`}
            </p>
          ))}
        </section>
      )}
    </form>
  );
});

/** What the cashier has to do with the money after a return. */
export function refundMessage(saved: SaleReturn): string {
  const parts = [`اتسجل مرتجع رقم ${saved.number}.`];
  if (saved.creditedPiasters > 0) parts.push(`اتخصم ${formatPiasters(saved.creditedPiasters)} من حساب العميل.`);
  if (saved.refundedPiasters > 0) {
    parts.push(
      saved.paymentMethod === "card"
        ? `رجّع ${formatPiasters(saved.refundedPiasters)} على الفيزا من الماكينة.`
        : `رجّع للعميل ${formatPiasters(saved.refundedPiasters)} كاش.`,
    );
  }
  return parts.join(" ");
}
