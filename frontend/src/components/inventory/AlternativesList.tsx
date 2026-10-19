"use client";

import { useEffect, useState } from "react";
import { api, type Alternatives, type DrugSummary } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters, formatQuantity } from "@/lib/format";

interface Props {
  drug: DrugSummary;
  /** Picking a stocked alternative: open it (inventory) or add it to the sale (POS). */
  onOpen: (id: string) => void;
  /** The POS uses a compact list that only shows what is on the shelf. */
  stockedOnly?: boolean;
}

/** Drugs with the same active ingredient: on our shelf, and in the Egyptian market for reference. */
export function AlternativesList({ drug, onOpen, stockedOnly = false }: Props) {
  const [data, setData] = useState<Alternatives | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api.getAlternatives(drug.id).then(
      (result) => !cancelled && setData(result),
      (e) => !cancelled && setError(errorMessage(e)),
    );
    return () => {
      cancelled = true;
    };
  }, [drug.id]);

  if (!drug.activeIngredient) {
    return stockedOnly ? (
      <p className="text-sm text-slate-500">مفيش مادة فعالة متسجلة للصنف ده، ضيفها من شاشة المخزون عشان تظهر البدائل.</p>
    ) : null;
  }

  return (
    <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
      <h3 className="mb-2 font-semibold">البدائل (نفس المادة الفعالة)</h3>
      {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}
      {!data && !error && <p className="text-sm text-slate-500">جاري التحميل…</p>}
      {data && (
        <div className="space-y-3 text-sm">
          {data.inStock.length === 0 ? (
            <p className="text-slate-500">مفيش بديل متسجل عندنا في المخزون.</p>
          ) : (
            <ul className="space-y-1">
              {data.inStock.map((alt) => (
                <li key={alt.id}>
                  <button
                    type="button"
                    onClick={() => onOpen(alt.id)}
                    className="flex w-full items-center justify-between gap-3 rounded-lg px-2 py-1 text-right hover:bg-slate-800"
                  >
                    <span>{alt.nameAr}</span>
                    <span className="flex shrink-0 gap-3">
                      <span className={alt.sellableQuantityInSubUnits > 0 ? "text-emerald-400" : "text-rose-400"}>
                        {alt.sellableQuantityInSubUnits > 0
                          ? formatQuantity(alt.sellableQuantityInSubUnits, alt.subUnitsPerPack, alt.packUnitName, alt.subUnitName)
                          : "نفد"}
                      </span>
                      <span className="font-mono" dir="ltr">{formatPiasters(alt.publicPricePiasters)}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          {!stockedOnly && data.inMarket.length > 0 && (
            <details>
              <summary className="cursor-pointer text-slate-400">في السوق ({data.inMarket.length}، الأرخص أولاً)</summary>
              <ul className="mt-1 space-y-1 text-slate-300">
                {data.inMarket.map((c) => (
                  <li key={c.nameEn} className="flex justify-between gap-3" dir="ltr">
                    <span>{c.nameEn}</span>
                    <span className="shrink-0 font-mono">{formatPiasters(c.publicPricePiasters)}</span>
                  </li>
                ))}
              </ul>
            </details>
          )}
        </div>
      )}
    </section>
  );
}
