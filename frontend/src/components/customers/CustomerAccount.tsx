"use client";

import { forwardRef, useState } from "react";
import { api, type Customer, type CustomerAccount as Account } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatCairoDateTime, formatPiasters, parsePounds } from "@/lib/format";

interface Props {
  account: Account;
  onPaid: (customer: Customer) => void;
}

/** A customer's balance, a form to take a payment, and their latest credit sales and payments. */
export const CustomerAccount = forwardRef<HTMLInputElement, Props>(function CustomerAccount({ account, onPaid }, amountRef) {
  const { customer, entries } = account;
  const [amountText, setAmountText] = useState("");
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const amount = parsePounds(amountText);
  const blocked =
    customer.balancePiasters === 0
      ? "مفيش فلوس على العميل"
      : amountText.trim() === ""
        ? "اكتب المبلغ اللي دفعه"
        : amount === null || amount === 0
          ? "اكتب المبلغ بالجنيه، مثلاً 100 أو 52.50"
          : amount > customer.balancePiasters
            ? "المبلغ أكبر من اللي على العميل"
            : null;

  const pay = async (event: React.FormEvent) => {
    event.preventDefault();
    if (blocked || amount === null || saving) return;
    setSaving(true);
    setMessage(null);
    try {
      const updated = await api.recordCustomerPayment(customer.id, amount, note.trim() || null);
      setAmountText("");
      setNote("");
      setMessage({ ok: true, text: `اتسجل ${formatPiasters(amount)}، الباقي عليه ${formatPiasters(updated.balancePiasters)}` });
      onPaid(updated);
    } catch (e) {
      setMessage({ ok: false, text: errorMessage(e) });
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="space-y-5 rounded-xl border border-slate-800 bg-slate-900 p-5">
      <header className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-bold">{customer.name}</h2>
          {customer.phone && <div className="font-mono text-sm text-slate-400" dir="ltr">{customer.phone}</div>}
        </div>
        <div className="text-left">
          <div className="text-sm text-slate-400">عليه</div>
          <div className="font-mono text-3xl font-bold text-amber-300" dir="ltr" data-testid="customer-balance">
            {formatPiasters(customer.balancePiasters)}
          </div>
        </div>
      </header>

      <form onSubmit={pay} className="grid gap-3 sm:grid-cols-[1fr_1.5fr_auto] sm:items-end" aria-label="تسجيل دفعة">
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">دفع كام؟ (F9)</span>
          <input
            ref={amountRef}
            value={amountText}
            onChange={(e) => setAmountText(e.target.value)}
            inputMode="decimal"
            placeholder={formatPiasters(customer.balancePiasters)}
            aria-label="مبلغ الدفعة"
            autoComplete="off"
            dir="ltr"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 font-mono text-lg"
          />
        </label>
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">ملاحظة (اختياري)</span>
          <input
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={200}
            aria-label="ملاحظة الدفعة"
            autoComplete="off"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
          />
        </label>
        <button
          type="submit"
          disabled={blocked !== null || saving}
          title={blocked ?? undefined}
          className="rounded-lg bg-emerald-600 px-5 py-2 font-bold text-white hover:bg-emerald-500 disabled:bg-slate-700 disabled:text-slate-400"
        >
          {saving ? "جاري الحفظ…" : "سجّل الدفعة"}
        </button>
      </form>
      {amountText.trim() !== "" && blocked && <p className="text-sm text-rose-400" data-testid="payment-blocked">{blocked}</p>}
      {message && (
        <p role={message.ok ? "status" : "alert"} className={message.ok ? "text-emerald-400" : "text-rose-400"}>
          {message.text}
        </p>
      )}

      <table className="w-full text-right">
        <thead className="text-sm text-slate-400">
          <tr>
            <th className="py-2">التاريخ</th>
            <th className="py-2">البيان</th>
            <th className="py-2">المبلغ</th>
          </tr>
        </thead>
        <tbody data-testid="customer-entries">
          {entries.length === 0 && (
            <tr>
              <td colSpan={3} className="py-6 text-center text-slate-500">لسه مفيش حركة على الحساب</td>
            </tr>
          )}
          {entries.map((entry) => (
            <tr key={`${entry.kind}-${entry.atUtc}-${entry.saleId ?? entry.note ?? ""}`} className="border-t border-slate-800">
              <td className="py-2 font-mono text-sm" dir="ltr">{formatCairoDateTime(entry.atUtc)}</td>
              <td className="py-2">
                {entry.kind === "sale" ? `فاتورة رقم ${entry.saleNumber}` : "دفعة"}
                {entry.note && <span className="text-sm text-slate-400"> · {entry.note}</span>}
              </td>
              <td className={`py-2 font-mono ${entry.kind === "sale" ? "text-amber-300" : "text-emerald-400"}`} dir="ltr">
                {entry.kind === "sale" ? "+" : "-"}
                {formatPiasters(entry.amountPiasters)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
});
