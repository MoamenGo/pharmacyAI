"use client";

import { forwardRef } from "react";
import { formatPiasters, formatQuantity } from "@/lib/format";
import { type CartLine, lineTotal, unitPrice } from "@/stores/cartStore";
import { QuantityInput } from "./QuantityInput";

interface Props {
  lines: CartLine[];
  selectedKey: string | null;
  shortDrugIds: Set<string>;
  onSelect: (key: string) => void;
  onQuantity: (key: string, quantity: number) => void;
  onToggleUnit: (key: string) => void;
  onRemove: (key: string) => void;
  /** Enter in a quantity box hands focus back to the search box for the next scan. */
  onDone: () => void;
}

/** The ref points at the selected line's quantity box, so F6 can jump straight to it. */
export const CartTable = forwardRef<HTMLInputElement, Props>(function CartTable(
  { lines, selectedKey, shortDrugIds, onSelect, onQuantity, onToggleUnit, onRemove, onDone },
  selectedQuantityRef,
) {
  if (lines.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-800 p-12 text-center text-slate-500">
        امسح باركود أول صنف لبدء الفاتورة
      </div>
    );
  }

  return (
    <table className="w-full overflow-hidden rounded-xl border border-slate-800 text-right">
      <thead className="bg-slate-900 text-sm text-slate-400">
        <tr>
          <th className="w-10 px-3 py-2">#</th>
          <th className="px-3 py-2">الصنف</th>
          <th className="w-32 px-3 py-2">الكمية</th>
          <th className="w-28 px-3 py-2">الوحدة</th>
          <th className="w-28 px-3 py-2">السعر</th>
          <th className="w-32 px-3 py-2">الإجمالي</th>
          <th className="w-12 px-3 py-2"><span className="sr-only">حذف</span></th>
        </tr>
      </thead>
      <tbody>
        {lines.map((line, i) => {
          const selected = line.key === selectedKey;
          const short = shortDrugIds.has(line.drug.id);
          const { drug } = line;
          return (
            <tr
              key={line.key}
              onClick={() => onSelect(line.key)}
              aria-selected={selected}
              className={`border-t border-slate-800 ${selected ? "bg-emerald-950/50" : ""} ${short ? "text-rose-300" : ""}`}
            >
              <td className="px-3 py-2 text-slate-500">{i + 1}</td>
              <td className="px-3 py-2">
                <div>{drug.nameAr}</div>
                <div className={`text-xs ${short ? "text-rose-400" : "text-slate-500"}`}>
                  {short ? "المطلوب أكبر من المتاح: " : "المتاح: "}
                  {drug.sellableQuantityInSubUnits > 0
                    ? formatQuantity(drug.sellableQuantityInSubUnits, drug.subUnitsPerPack, drug.packUnitName, drug.subUnitName)
                    : "لا يوجد"}
                  {short && " · F11 للبدائل"}
                </div>
              </td>
              <td className="px-3 py-2">
                <QuantityInput
                  ref={selected ? selectedQuantityRef : undefined}
                  value={line.quantity}
                  label={`كمية ${drug.nameAr}`}
                  onFocus={() => onSelect(line.key)}
                  onChange={(quantity) => onQuantity(line.key, quantity)}
                  onEnter={onDone}
                />
              </td>
              <td className="px-3 py-2">
                {drug.subUnitsPerPack > 1 ? (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onToggleUnit(line.key);
                    }}
                    className="rounded-lg border border-slate-700 px-2 py-1 text-sm hover:border-emerald-500"
                    title="تبديل علبة / شريط (F4)"
                  >
                    {line.unit === "pack" ? drug.packUnitName : drug.subUnitName}
                  </button>
                ) : (
                  drug.packUnitName
                )}
              </td>
              <td className="whitespace-nowrap px-3 py-2 font-mono text-sm" dir="ltr">{formatPiasters(unitPrice(line))}</td>
              <td className="whitespace-nowrap px-3 py-2 font-mono" dir="ltr">{formatPiasters(lineTotal(line))}</td>
              <td className="px-3 py-2">
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    onRemove(line.key);
                  }}
                  className="rounded px-2 text-slate-500 hover:text-rose-400"
                  aria-label={`حذف ${drug.nameAr}`}
                >
                  ✕
                </button>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
});
