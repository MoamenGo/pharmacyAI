"use client";

import { forwardRef, useState } from "react";
import type { DrugDetails } from "@/lib/api";
import { formatPiasters, formatQuantity } from "@/lib/format";
import { AlternativesList } from "./AlternativesList";
import { ExpiryBadge } from "./ExpiryBadge";
import { ReceiveStockForm } from "./ReceiveStockForm";
import { WriteOffForm } from "./WriteOffForm";

interface Props {
  details: DrugDetails;
  onChanged: (details: DrugDetails) => void;
  onEdit: () => void;
  onOpenDrug: (id: string) => void;
}

export const DrugPanel = forwardRef<HTMLInputElement, Props>(function DrugPanel({ details, onChanged, onEdit, onOpenDrug }, receiveRef) {
  const { drug, batches } = details;
  const [writingOff, setWritingOff] = useState<string | null>(null);
  const qty = (n: number) => formatQuantity(n, drug.subUnitsPerPack, drug.packUnitName, drug.subUnitName);

  return (
    <aside className="space-y-4">
      <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h2 className="text-xl font-bold">{drug.nameAr}</h2>
            <p className="text-sm text-slate-500" dir="ltr">{drug.nameEn}</p>
          </div>
          <button type="button" onClick={onEdit} className="shrink-0 rounded-lg border border-slate-700 px-3 py-1 text-sm hover:border-emerald-500">
            تعديل (F6)
          </button>
        </div>
        <dl className="mt-3 grid grid-cols-2 gap-2 text-sm">
          <dt className="text-slate-400">سعر ال{drug.packUnitName}</dt>
          <dd>{formatPiasters(drug.publicPricePiasters)}</dd>
          {drug.subUnitsPerPack > 1 && (
            <>
              <dt className="text-slate-400">سعر ال{drug.subUnitName}</dt>
              <dd>{formatPiasters(drug.subUnitPricePiasters)}</dd>
            </>
          )}
          <dt className="text-slate-400">الرصيد الصالح</dt>
          <dd className={drug.isShort ? "text-amber-300" : ""}>
            {qty(drug.sellableQuantityInSubUnits)}
            {drug.isShort && " · محتاج طلب"}
          </dd>
          {drug.reorderLevelInPacks > 0 && (
            <>
              <dt className="text-slate-400">حد الطلب</dt>
              <dd>
                {drug.reorderLevelInPacks} {drug.packUnitName}
              </dd>
            </>
          )}
          {drug.expiredQuantityInSubUnits > 0 && (
            <>
              <dt className="text-rose-400">منتهي الصلاحية</dt>
              <dd className="text-rose-400">{qty(drug.expiredQuantityInSubUnits)}</dd>
            </>
          )}
          {drug.activeIngredient && (
            <>
              <dt className="text-slate-400">المادة الفعالة</dt>
              <dd dir="ltr" className="text-right text-xs">{drug.activeIngredient}</dd>
            </>
          )}
          {drug.manufacturer && (
            <>
              <dt className="text-slate-400">الشركة</dt>
              <dd dir="ltr" className="text-right text-xs">{drug.manufacturer}</dd>
            </>
          )}
        </dl>
      </section>

      <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
        <h3 className="mb-2 font-semibold">التشغيلات (الأقرب انتهاءً أولاً)</h3>
        {batches.length === 0 ? (
          <p className="text-sm text-slate-500">لا يوجد رصيد</p>
        ) : (
          <ul className="space-y-2">
            {batches.map((batch) => (
              <li key={batch.id} className="text-sm">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-mono" dir="ltr">{batch.batchNumber}</span>
                  <ExpiryBadge date={batch.expiryDate} expired={batch.isExpired} />
                  <span>{qty(batch.quantityInSubUnits)}</span>
                  <button
                    type="button"
                    onClick={() => setWritingOff(writingOff === batch.id ? null : batch.id)}
                    className={`rounded px-2 py-0.5 text-xs ${batch.isExpired ? "bg-rose-800 text-rose-100 hover:bg-rose-700" : "text-slate-400 hover:text-rose-300"}`}
                  >
                    إعدام
                  </button>
                </div>
                {writingOff === batch.id && (
                  <WriteOffForm
                    drug={drug}
                    batch={batch}
                    onCancel={() => setWritingOff(null)}
                    onDone={(updated) => {
                      setWritingOff(null);
                      onChanged(updated);
                    }}
                  />
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <ReceiveStockForm key={drug.id} ref={receiveRef} drug={drug} onReceived={onChanged} />

      <AlternativesList key={`alt-${drug.id}`} drug={drug} onOpen={onOpenDrug} />
    </aside>
  );
});
