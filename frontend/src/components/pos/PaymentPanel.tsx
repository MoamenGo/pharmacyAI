"use client";

import type { RefObject } from "react";
import { CustomerPicker } from "@/components/customers/CustomerPicker";
import type { Customer, PaymentMethod } from "@/lib/api";
import { formatPiasters } from "@/lib/format";

export const PAYMENT_METHODS: { value: PaymentMethod; label: string }[] = [
  { value: "cash", label: "نقدي" },
  { value: "card", label: "فيزا" },
  { value: "credit", label: "آجل" },
];

interface Props {
  subtotalPiasters: number;
  /** Null while the discount box holds something that is not an amount or a percentage. */
  discountPiasters: number | null;
  totalPiasters: number;
  itemCount: number;
  method: PaymentMethod;
  onMethod: (method: PaymentMethod) => void;
  discountText: string;
  onDiscountText: (text: string) => void;
  paidText: string;
  /** Null while the box is empty: cash pays exactly the total, credit pays nothing now. */
  paidPiasters: number | null;
  onPaidText: (text: string) => void;
  cardReference: string;
  onCardReference: (text: string) => void;
  customer: Customer | null;
  onCustomer: (customer: Customer | null) => void;
  refs: {
    paid: RefObject<HTMLInputElement>;
    discount: RefObject<HTMLInputElement>;
    customer: RefObject<HTMLInputElement>;
  };
  blockedReason: string | null;
  saving: boolean;
  error: string | null;
  onSubmit: () => void;
}

const submitOnEnter = (onSubmit: () => void) => (e: React.KeyboardEvent) => {
  if (e.key === "Enter") {
    e.preventDefault();
    onSubmit();
  }
};

const inputClass = (invalid: boolean) =>
  `w-full rounded-lg border bg-slate-950 px-3 py-2 font-mono text-2xl ${invalid ? "border-rose-600" : "border-slate-700"}`;

export function PaymentPanel(props: Props) {
  const { subtotalPiasters, discountPiasters, totalPiasters, itemCount, method, paidText, paidPiasters, customer, refs } = props;
  const discounted = (discountPiasters ?? 0) > 0;

  return (
    <aside className="space-y-4 rounded-xl border border-slate-800 bg-slate-900 p-5">
      <div>
        <div className="text-sm text-slate-400">الإجمالي ({itemCount} صنف)</div>
        <div className="font-mono text-4xl font-bold text-emerald-400" dir="ltr" data-testid="sale-total">
          {formatPiasters(totalPiasters)}
        </div>
        {discounted && (
          <div className="text-sm text-slate-400" data-testid="sale-subtotal">
            قبل الخصم <span className="font-mono line-through" dir="ltr">{formatPiasters(subtotalPiasters)}</span>
          </div>
        )}
      </div>

      <label className="block">
        <span className="mb-1 block text-sm text-slate-400">خصم (F7) بالجنيه أو بالنسبة، مثلاً 5 أو 10%</span>
        <input
          ref={refs.discount}
          value={props.discountText}
          onChange={(e) => props.onDiscountText(e.target.value)}
          onKeyDown={submitOnEnter(props.onSubmit)}
          placeholder="0"
          className={inputClass(discountPiasters === null).replace("text-2xl", "text-lg")}
          aria-label="الخصم"
          autoComplete="off"
          dir="ltr"
        />
      </label>

      <div>
        <span className="mb-1 block text-sm text-slate-400">طريقة الدفع (F5)</span>
        <div className="grid grid-cols-3 gap-1 rounded-lg bg-slate-950 p-1" role="radiogroup" aria-label="طريقة الدفع">
          {PAYMENT_METHODS.map((m) => (
            <button
              key={m.value}
              type="button"
              role="radio"
              aria-checked={method === m.value}
              onClick={() => props.onMethod(m.value)}
              className={`rounded-md py-2 font-semibold ${method === m.value ? "bg-emerald-600 text-white" : "text-slate-400 hover:text-white"}`}
            >
              {m.label}
            </button>
          ))}
        </div>
      </div>

      {method === "cash" && (
        <>
          <label className="block">
            <span className="mb-1 block text-sm text-slate-400">المدفوع (فاضي = المبلغ بالظبط)</span>
            <input
              ref={refs.paid}
              value={paidText}
              onChange={(e) => props.onPaidText(e.target.value)}
              onKeyDown={submitOnEnter(props.onSubmit)}
              inputMode="decimal"
              placeholder={formatPiasters(totalPiasters)}
              className={inputClass(paidText.trim() !== "" && paidPiasters === null)}
              aria-label="المبلغ المدفوع"
              autoComplete="off"
              dir="ltr"
            />
          </label>
          <Row label="الباقي" testId="sale-change" value={(paidPiasters ?? totalPiasters) - totalPiasters} warn />
        </>
      )}

      {method === "card" && (
        <label className="block">
          <span className="mb-1 block text-sm text-slate-400">رقم العملية من الماكينة (اختياري)</span>
          <input
            ref={refs.paid}
            value={props.cardReference}
            onChange={(e) => props.onCardReference(e.target.value)}
            onKeyDown={submitOnEnter(props.onSubmit)}
            maxLength={50}
            className={inputClass(false).replace("text-2xl", "text-lg")}
            aria-label="رقم عملية الفيزا"
            autoComplete="off"
            dir="ltr"
          />
        </label>
      )}

      {method === "credit" && (
        <>
          <CustomerPicker ref={refs.customer} customer={customer} onChange={props.onCustomer} />
          <label className="block">
            <span className="mb-1 block text-sm text-slate-400">المدفوع دلوقتي (فاضي = ولا حاجة)</span>
            <input
              ref={refs.paid}
              value={paidText}
              onChange={(e) => props.onPaidText(e.target.value)}
              onKeyDown={submitOnEnter(props.onSubmit)}
              inputMode="decimal"
              placeholder="0.00"
              className={inputClass(paidText.trim() !== "" && paidPiasters === null)}
              aria-label="المدفوع دلوقتي"
              autoComplete="off"
              dir="ltr"
            />
          </label>
          <Row label="على الحساب" testId="sale-due" value={totalPiasters - (paidPiasters ?? 0)} warn />
        </>
      )}

      {(props.error ?? props.blockedReason) && (
        <p role="alert" className="text-rose-400">
          {props.error ?? props.blockedReason}
        </p>
      )}

      <button
        type="button"
        onClick={props.onSubmit}
        disabled={props.saving || props.blockedReason !== null}
        className="w-full rounded-xl bg-emerald-600 py-3 text-lg font-bold text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-400"
      >
        {props.saving ? "جاري الحفظ…" : "حفظ وطباعة (F10)"}
      </button>
    </aside>
  );
}

function Row({ label, value, testId, warn }: { label: string; value: number; testId: string; warn?: boolean }) {
  return (
    <div className="flex items-baseline justify-between">
      <span className="text-slate-400">{label}</span>
      <span className={`font-mono text-2xl ${value < 0 ? "text-rose-400" : warn ? "text-amber-300" : ""}`} dir="ltr" data-testid={testId}>
        {formatPiasters(value)}
      </span>
    </div>
  );
}
