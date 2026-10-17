"use client";

import { forwardRef, useState } from "react";
import { api, type DrugDetails } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { endOfMonth } from "@/lib/format";
import { Field } from "./Field";

interface Props {
  drug: DrugDetails["drug"];
  onReceived: (details: DrugDetails) => void;
}

export const ReceiveStockForm = forwardRef<HTMLInputElement, Props>(function ReceiveStockForm({ drug, onReceived }, batchRef) {
  const [batchNumber, setBatchNumber] = useState("");
  const [expiryMonth, setExpiryMonth] = useState("");
  const [packs, setPacks] = useState("1");
  const [loose, setLoose] = useState("0");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!expiryMonth) {
      setError("اختار شهر وسنة الصلاحية");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const details = await api.receiveStock(drug.id, {
        batchNumber: batchNumber.trim(),
        expiryDate: endOfMonth(expiryMonth),
        packs: Number(packs) || 0,
        looseSubUnits: Number(loose) || 0,
      });
      onReceived(details);
      setBatchNumber("");
      setExpiryMonth("");
      setPacks("1");
      setLoose("0");
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-3 rounded-xl border border-slate-800 bg-slate-900 p-4">
      <h3 className="font-semibold">استلام كمية</h3>
      <div className="grid grid-cols-2 gap-3">
        <Field ref={batchRef} id="batch" label="رقم التشغيلة" value={batchNumber} onChange={(e) => setBatchNumber(e.target.value)} required dir="ltr" />
        <Field id="expiry" label="الصلاحية (شهر/سنة)" type="month" value={expiryMonth} onChange={(e) => setExpiryMonth(e.target.value)} required dir="ltr" />
        <Field id="packs" label={`عدد ${drug.packUnitName}`} type="number" min={0} value={packs} onChange={(e) => setPacks(e.target.value)} />
        {drug.subUnitsPerPack > 1 && (
          <Field
            id="loose"
            label={`${drug.subUnitName} فرط`}
            type="number"
            min={0}
            max={drug.subUnitsPerPack - 1}
            value={loose}
            onChange={(e) => setLoose(e.target.value)}
          />
        )}
      </div>
      <div className="flex items-center gap-3">
        <button type="submit" disabled={saving} className="rounded-lg bg-emerald-600 px-5 py-2 font-semibold hover:bg-emerald-500 disabled:opacity-50">
          استلام
        </button>
        {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}
      </div>
    </form>
  );
});
