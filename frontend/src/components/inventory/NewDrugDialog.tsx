"use client";

import { useEffect, useRef, useState } from "react";
import { api, type DrugDetails } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { parsePounds } from "@/lib/format";
import { Field } from "./Field";

interface Props {
  initialBarcode: string;
  onClose: () => void;
  onCreated: (details: DrugDetails) => void;
}

export function NewDrugDialog({ initialBarcode, onClose, onCreated }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [form, setForm] = useState({
    nameAr: "",
    nameEn: "",
    barcode: initialBarcode,
    packUnitName: "علبة",
    subUnitName: "شريط",
    subUnitsPerPack: "1",
    price: "",
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [key]: e.target.value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const price = parsePounds(form.price);
    if (price === null || price <= 0) {
      setError("اكتب السعر بالجنيه، مثلاً 45 أو 45.50");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const created = await api.createDrug({
        nameAr: form.nameAr.trim(),
        nameEn: form.nameEn.trim() || null,
        barcode: form.barcode.trim(),
        packUnitName: form.packUnitName.trim(),
        subUnitName: form.subUnitName.trim(),
        subUnitsPerPack: Number(form.subUnitsPerPack) || 1,
        publicPricePiasters: price,
      });
      onCreated(created);
    } catch (e) {
      setError(errorMessage(e));
      setSaving(false);
    }
  };

  return (
    <dialog
      ref={dialogRef}
      onClose={onClose}
      className="w-full max-w-xl rounded-xl border border-slate-700 bg-slate-900 p-6 text-slate-100 backdrop:bg-black/70"
    >
      <form onSubmit={submit} className="space-y-4">
        <h2 className="text-xl font-bold">صنف جديد</h2>
        <div className="grid grid-cols-2 gap-3">
          <Field id="nameAr" label="الاسم بالعربي" value={form.nameAr} onChange={set("nameAr")} required autoFocus className="col-span-2" />
          <Field id="nameEn" label="الاسم بالإنجليزي" value={form.nameEn} onChange={set("nameEn")} dir="ltr" className="col-span-2" />
          <Field id="barcode" label="الباركود" value={form.barcode} onChange={set("barcode")} required dir="ltr" />
          <Field id="price" label="سعر العلبة (ج.م)" value={form.price} onChange={set("price")} required inputMode="decimal" dir="ltr" />
          <Field id="packUnitName" label="اسم العبوة" value={form.packUnitName} onChange={set("packUnitName")} required />
          <Field id="subUnitName" label="اسم الوحدة الصغرى" value={form.subUnitName} onChange={set("subUnitName")} required />
          <Field id="subUnitsPerPack" label="عدد الوحدات في العبوة" type="number" min={1} value={form.subUnitsPerPack} onChange={set("subUnitsPerPack")} required />
        </div>
        {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}
        <div className="flex gap-3">
          <button type="submit" disabled={saving} className="rounded-lg bg-emerald-600 px-6 py-2 font-semibold hover:bg-emerald-500 disabled:opacity-50">
            حفظ
          </button>
          <button type="button" onClick={() => dialogRef.current?.close()} className="rounded-lg border border-slate-700 px-6 py-2">
            إلغاء <kbd className="mr-1 text-xs text-slate-500">Esc</kbd>
          </button>
        </div>
      </form>
    </dialog>
  );
}
