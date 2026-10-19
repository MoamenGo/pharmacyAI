"use client";

import { useEffect, useRef, useState } from "react";
import { api, type DrugDetails } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPounds, parsePounds } from "@/lib/format";
import { Field } from "./Field";

interface Props {
  drug: DrugDetails["drug"];
  onClose: () => void;
  onSaved: (details: DrugDetails) => void;
}

/** Corrects a drug's names, barcode, price and reorder level. The pack size is fixed once the drug exists. */
export function EditDrugDialog({ drug, onClose, onSaved }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [form, setForm] = useState({
    nameAr: drug.nameAr,
    nameEn: drug.nameEn ?? "",
    barcode: drug.barcode,
    price: formatPounds(drug.publicPricePiasters),
    activeIngredient: drug.activeIngredient ?? "",
    manufacturer: drug.manufacturer ?? "",
    reorderLevel: String(drug.reorderLevelInPacks),
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
      onSaved(
        await api.updateDrug(drug.id, {
          nameAr: form.nameAr.trim(),
          nameEn: form.nameEn.trim() || null,
          barcode: form.barcode.trim(),
          publicPricePiasters: price,
          activeIngredient: form.activeIngredient.trim() || null,
          manufacturer: form.manufacturer.trim() || null,
          reorderLevelInPacks: Math.max(0, Math.trunc(Number(form.reorderLevel) || 0)),
        }),
      );
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
        <h2 className="text-xl font-bold">تعديل الصنف</h2>
        <div className="grid grid-cols-2 gap-3">
          <Field id="edit-nameAr" label="الاسم بالعربي" value={form.nameAr} onChange={set("nameAr")} required autoFocus className="col-span-2" />
          <Field id="edit-nameEn" label="الاسم بالإنجليزي" value={form.nameEn} onChange={set("nameEn")} dir="ltr" className="col-span-2" />
          <Field id="edit-activeIngredient" label="المادة الفعالة" value={form.activeIngredient} onChange={set("activeIngredient")} dir="ltr" />
          <Field id="edit-manufacturer" label="الشركة" value={form.manufacturer} onChange={set("manufacturer")} dir="ltr" />
          <Field id="edit-barcode" label="الباركود" value={form.barcode} onChange={set("barcode")} required dir="ltr" />
          <Field id="edit-price" label={`سعر ال${drug.packUnitName} (ج.م)`} value={form.price} onChange={set("price")} required inputMode="decimal" dir="ltr" />
          <Field
            id="edit-reorderLevel"
            label={`حد الطلب (عدد ال${drug.packUnitName}، 0 = من غير تنبيه)`}
            type="number"
            min={0}
            value={form.reorderLevel}
            onChange={set("reorderLevel")}
            className="col-span-2"
          />
        </div>
        <p className="text-sm text-slate-500">
          العبوة: {drug.packUnitName} فيها {drug.subUnitsPerPack} {drug.subUnitName}. حجم العبوة مبيتغيرش بعد الإضافة عشان الرصيد ميتلخبطش. تغيير السعر مبيأثرش على الفواتير القديمة.
        </p>
        {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}
        <div className="flex gap-3">
          <button type="submit" disabled={saving} className="rounded-lg bg-emerald-600 px-6 py-2 font-semibold hover:bg-emerald-500 disabled:opacity-50">
            حفظ التعديل
          </button>
          <button type="button" onClick={() => dialogRef.current?.close()} className="rounded-lg border border-slate-700 px-6 py-2">
            إلغاء <kbd className="mr-1 text-xs text-slate-500">Esc</kbd>
          </button>
        </div>
      </form>
    </dialog>
  );
}
