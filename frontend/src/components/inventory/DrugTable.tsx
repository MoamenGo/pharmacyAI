import type { DrugSummary } from "@/lib/api";
import { formatPiasters, formatQuantity } from "@/lib/format";
import { ExpiryBadge } from "./ExpiryBadge";

interface Props {
  drugs: DrugSummary[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}

export function DrugTable({ drugs, selectedId, onSelect }: Props) {
  if (drugs.length === 0) {
    return <p className="rounded-xl border border-slate-800 bg-slate-900 p-8 text-center text-slate-400">لا توجد أصناف. اضغط F2 لإضافة صنف جديد.</p>;
  }

  return (
    <div className="overflow-hidden rounded-xl border border-slate-800">
      <table className="w-full text-right">
        <thead className="bg-slate-900 text-sm text-slate-400">
          <tr>
            <th className="px-4 py-3 font-medium">الصنف</th>
            <th className="px-4 py-3 font-medium">السعر</th>
            <th className="px-4 py-3 font-medium">الرصيد</th>
            <th className="px-4 py-3 font-medium">أقرب صلاحية</th>
          </tr>
        </thead>
        <tbody>
          {drugs.map((drug) => (
            <tr
              key={drug.id}
              tabIndex={0}
              onClick={() => onSelect(drug.id)}
              onKeyDown={(e) => e.key === "Enter" && onSelect(drug.id)}
              className={`cursor-pointer border-t border-slate-800 ${drug.id === selectedId ? "bg-emerald-950/50" : "hover:bg-slate-900"}`}
            >
              <td className="px-4 py-3">
                <div className="font-semibold">{drug.nameAr}</div>
                <div className="text-sm text-slate-500" dir="ltr">
                  {drug.nameEn ?? ""} · {drug.barcode}
                </div>
              </td>
              <td className="px-4 py-3 whitespace-nowrap">{formatPiasters(drug.publicPricePiasters)}</td>
              <td className={`px-4 py-3 ${drug.sellableQuantityInSubUnits === 0 ? "text-rose-400" : drug.isShort ? "text-amber-300" : ""}`}>
                {formatQuantity(drug.sellableQuantityInSubUnits, drug.subUnitsPerPack, drug.packUnitName, drug.subUnitName)}
                {drug.isShort && <div className="text-xs">محتاج طلب</div>}
              </td>
              <td className="px-4 py-3">{drug.nearestExpiry ? <ExpiryBadge date={drug.nearestExpiry} /> : <span className="text-slate-600">—</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
