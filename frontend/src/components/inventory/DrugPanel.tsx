import { forwardRef } from "react";
import type { DrugDetails } from "@/lib/api";
import { formatPiasters, formatQuantity } from "@/lib/format";
import { ExpiryBadge } from "./ExpiryBadge";
import { ReceiveStockForm } from "./ReceiveStockForm";

interface Props {
  details: DrugDetails;
  onChanged: (details: DrugDetails) => void;
}

export const DrugPanel = forwardRef<HTMLInputElement, Props>(function DrugPanel({ details, onChanged }, receiveRef) {
  const { drug, batches } = details;
  const qty = (n: number) => formatQuantity(n, drug.subUnitsPerPack, drug.packUnitName, drug.subUnitName);

  return (
    <aside className="space-y-4">
      <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
        <h2 className="text-xl font-bold">{drug.nameAr}</h2>
        <p className="text-sm text-slate-500" dir="ltr">{drug.nameEn}</p>
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
          <dd>{qty(drug.sellableQuantityInSubUnits)}</dd>
          {drug.expiredQuantityInSubUnits > 0 && (
            <>
              <dt className="text-rose-400">منتهي الصلاحية</dt>
              <dd className="text-rose-400">{qty(drug.expiredQuantityInSubUnits)}</dd>
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
              <li key={batch.id} className="flex items-center justify-between gap-2 text-sm">
                <span className="font-mono" dir="ltr">{batch.batchNumber}</span>
                <ExpiryBadge date={batch.expiryDate} expired={batch.isExpired} />
                <span>{qty(batch.quantityInSubUnits)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <ReceiveStockForm key={drug.id} ref={receiveRef} drug={drug} onReceived={onChanged} />
    </aside>
  );
});
