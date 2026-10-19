"use client";

import { useEffect, useRef, useState } from "react";
import { api, type CatalogDrug, type DrugDetails } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPounds, parsePounds } from "@/lib/format";
import { CatalogPicker, unitsForRoute } from "./CatalogPicker";
import { Field } from "./Field";

interface Props {
  initialBarcode: string;
  onClose: () => void;
  onCreated: (details: DrugDetails) => void;
}

/**
 * The list's Arabic name is the brand only ("كونكور"), which would make 2.5 mg and 5 mg look the same
 * on a receipt; keep the strength and pack size from the English name ("كونكور 5 MG 30 F.C. TABS.").
 */
function arabicName(drug: CatalogDrug): string {
  const detail = /\s(\d.*)$/.exec(drug.nameEn)?.[1];
  // The Latin part is wrapped in a left-to-right isolate so it reads in order inside Arabic text.
  return detail && !drug.nameAr.includes(detail) ? `${drug.nameAr} \u2066${detail}\u2069` : drug.nameAr;
}

export function NewDrugDialog({ initialBarcode, onClose, onCreated }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const subUnitsRef = useRef<HTMLInputElement>(null);
  const [form, setForm] = useState({
    nameAr: "",
    nameEn: "",
    barcode: initialBarcode,
    packUnitName: "علبة",
    subUnitName: "شريط",
    subUnitsPerPack: "1",
    price: "",
    activeIngredient: "",
    manufacturer: "",
    reorderLevel: "0",
  });
  const [fromCatalog, setFromCatalog] = useState<CatalogDrug | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  // The list never says how many strips a box holds: that is the one thing left to type.
  useEffect(() => {
    if (fromCatalog) subUnitsRef.current?.select();
  }, [fromCatalog]);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [key]: e.target.value }));

  const fill = (drug: CatalogDrug) => {
    setFromCatalog(drug);
    setForm((f) => ({
      ...f,
      nameEn: drug.nameEn,
      nameAr: arabicName(drug),
      price: drug.pricePiasters !== null ? formatPounds(drug.pricePiasters) : f.price,
      activeIngredient: drug.activeIngredient ?? "",
      manufacturer: drug.manufacturer ?? "",
      ...unitsForRoute(drug.route),
    }));
  };

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
        activeIngredient: form.activeIngredient.trim() || null,
        manufacturer: form.manufacturer.trim() || null,
        reorderLevelInPacks: Math.max(0, Math.trunc(Number(form.reorderLevel) || 0)),
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
      className="w-full max-w-2xl rounded-xl border border-slate-700 bg-slate-900 p-6 text-slate-100 backdrop:bg-black/70"
    >
      <form onSubmit={submit} className="space-y-4">
        <h2 className="text-xl font-bold">صنف جديد</h2>
        <CatalogPicker onPick={fill} autoFocus />
        {fromCatalog && (
          <p role="status" className="rounded-lg bg-emerald-950 px-3 py-2 text-sm text-emerald-300">
            اتملت البيانات من الدليل. راجع السعر (ممكن يكون اتغير) واكتب عدد الوحدات في العبوة والباركود.
          </p>
        )}
        <div className="grid grid-cols-2 gap-3">
          <Field id="nameAr" label="الاسم بالعربي" value={form.nameAr} onChange={set("nameAr")} required className="col-span-2" />
          <Field id="nameEn" label="الاسم بالإنجليزي" value={form.nameEn} onChange={set("nameEn")} dir="ltr" className="col-span-2" />
          <Field id="activeIngredient" label="المادة الفعالة" value={form.activeIngredient} onChange={set("activeIngredient")} dir="ltr" />
          <Field id="manufacturer" label="الشركة" value={form.manufacturer} onChange={set("manufacturer")} dir="ltr" />
          <Field id="barcode" label="الباركود" value={form.barcode} onChange={set("barcode")} required dir="ltr" />
          <Field id="price" label="سعر العبوة (ج.م)" value={form.price} onChange={set("price")} required inputMode="decimal" dir="ltr" />
          <Field id="packUnitName" label="اسم العبوة" value={form.packUnitName} onChange={set("packUnitName")} required />
          <Field id="subUnitName" label="اسم الوحدة الصغرى" value={form.subUnitName} onChange={set("subUnitName")} required />
          <Field
            ref={subUnitsRef}
            id="subUnitsPerPack"
            label="عدد الوحدات في العبوة"
            type="number"
            min={1}
            value={form.subUnitsPerPack}
            onChange={set("subUnitsPerPack")}
            required
          />
          <Field
            id="reorderLevel"
            label="حد الطلب (عدد العبوات، 0 = من غير تنبيه)"
            type="number"
            min={0}
            value={form.reorderLevel}
            onChange={set("reorderLevel")}
          />
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
