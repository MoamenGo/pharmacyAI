"use client";

import { useState } from "react";
import { api, type DrugDetails, type StockBatch, type WriteOffReason } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatQuantity } from "@/lib/format";

export const WRITE_OFF_REASONS: Record<WriteOffReason, string> = {
  expired: "منتهي الصلاحية",
  damaged: "تالف / مكسور",
  lost: "مفقود",
  other: "سبب تاني",
};

interface Props {
  drug: DrugDetails["drug"];
  batch: StockBatch;
  onDone: (details: DrugDetails) => void;
  onCancel: () => void;
}

/** Takes stock out of one batch without a sale (إعدام), defaulting to the whole batch. */
export function WriteOffForm({ drug, batch, onDone, onCancel }: Props) {
  const perPack = drug.subUnitsPerPack;
  const [packs, setPacks] = useState(String(Math.trunc(batch.quantityInSubUnits / perPack)));
  const [loose, setLoose] = useState(String(batch.quantityInSubUnits % perPack));
  const [reason, setReason] = useState<WriteOffReason>(batch.isExpired ? "expired" : "damaged");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const quantity = (Number(packs) || 0) * perPack + (perPack > 1 ? Number(loose) || 0 : 0);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (quantity <= 0 || quantity > batch.quantityInSubUnits) {
      setError(`الكمية لازم تكون بين 1 و ${formatQuantity(batch.quantityInSubUnits, perPack, drug.packUnitName, drug.subUnitName)}`);
      return;
    }
    setSaving(true);
    setError(null);
    try {
      onDone(await api.writeOff(drug.id, batch.id, { quantityInSubUnits: quantity, reason, note: note.trim() || null }));
    } catch (e) {
      setError(errorMessage(e));
      setSaving(false);
    }
  };

  const input = "w-full rounded-lg border border-slate-700 bg-slate-950 px-2 py-1";

  return (
    <form onSubmit={submit} className="mt-2 space-y-2 rounded-lg border border-rose-900 bg-rose-950/30 p-3 text-sm">
      <div className="font-semibold text-rose-300">
        إعدام من التشغيلة <span dir="ltr" className="font-mono">{batch.batchNumber}</span>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <label>
          <span className="text-slate-400">عدد ال{drug.packUnitName}</span>
          <input type="number" min={0} value={packs} onChange={(e) => setPacks(e.target.value)} className={input} autoFocus />
        </label>
        {perPack > 1 && (
          <label>
            <span className="text-slate-400">{drug.subUnitName} فرط</span>
            <input type="number" min={0} max={perPack - 1} value={loose} onChange={(e) => setLoose(e.target.value)} className={input} />
          </label>
        )}
        <label>
          <span className="text-slate-400">السبب</span>
          <select value={reason} onChange={(e) => setReason(e.target.value as WriteOffReason)} className={input}>
            {Object.entries(WRITE_OFF_REASONS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span className="text-slate-400">ملاحظة</span>
          <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} className={input} />
        </label>
      </div>
      {error && <p role="alert" className="text-rose-400">{error}</p>}
      <div className="flex gap-2">
        <button type="submit" disabled={saving} className="rounded-lg bg-rose-700 px-4 py-1 font-semibold hover:bg-rose-600 disabled:opacity-50">
          تأكيد الإعدام
        </button>
        <button type="button" onClick={onCancel} className="rounded-lg border border-slate-700 px-4 py-1">
          إلغاء
        </button>
      </div>
    </form>
  );
}
