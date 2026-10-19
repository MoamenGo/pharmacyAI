"use client";

import { useEffect, useState } from "react";
import { Field } from "@/components/inventory/Field";
import { api, type EtaSettings, type EtaSettingsInput } from "@/lib/api";
import { errorMessage } from "@/lib/errors";

/** Arabic names for the API's field names, also used to list what is missing. */
export const ETA_FIELD_LABELS: Record<string, string> = {
  ClientId: "Client ID",
  ClientSecret: "Client Secret",
  PosSerial: "رقم جهاز نقطة البيع (POS Serial)",
  PosOsVersion: "نظام تشغيل الجهاز (OS Version)",
  PosModelFramework: "Model Framework",
  PresharedKey: "Preshared Key",
  Rin: "رقم التسجيل الضريبي",
  CompanyTradeName: "الاسم التجاري",
  BranchCode: "كود الفرع",
  Governate: "المحافظة",
  RegionCity: "المدينة / الحي",
  Street: "الشارع",
  BuildingNumber: "رقم المبنى",
  ActivityCode: "كود النشاط",
};

const EMPTY: EtaSettingsInput = {
  enabled: false,
  environment: "preProduction",
  clientId: "",
  clientSecret: null,
  posSerial: "",
  posOsVersion: "",
  posModelFramework: "",
  presharedKey: null,
  rin: "",
  companyTradeName: "",
  branchCode: "0",
  governate: "",
  regionCity: "",
  street: "",
  buildingNumber: "",
  activityCode: "4772",
};

type TextKey = Exclude<keyof EtaSettingsInput, "enabled" | "environment" | "clientSecret" | "presharedKey">;

const SECTIONS: { title: string; fields: { key: TextKey; label: string; dir?: "ltr" }[] }[] = [
  {
    title: "بيانات الدخول من بوابة الإيصال الإلكتروني",
    fields: [
      { key: "clientId", label: ETA_FIELD_LABELS.ClientId, dir: "ltr" },
      { key: "posSerial", label: ETA_FIELD_LABELS.PosSerial, dir: "ltr" },
      { key: "posOsVersion", label: ETA_FIELD_LABELS.PosOsVersion, dir: "ltr" },
      { key: "posModelFramework", label: ETA_FIELD_LABELS.PosModelFramework, dir: "ltr" },
    ],
  },
  {
    title: "بيانات الصيدلية",
    fields: [
      { key: "rin", label: ETA_FIELD_LABELS.Rin, dir: "ltr" },
      { key: "companyTradeName", label: ETA_FIELD_LABELS.CompanyTradeName },
      { key: "branchCode", label: ETA_FIELD_LABELS.BranchCode, dir: "ltr" },
      { key: "activityCode", label: ETA_FIELD_LABELS.ActivityCode, dir: "ltr" },
      { key: "governate", label: ETA_FIELD_LABELS.Governate },
      { key: "regionCity", label: ETA_FIELD_LABELS.RegionCity },
      { key: "street", label: ETA_FIELD_LABELS.Street },
      { key: "buildingNumber", label: ETA_FIELD_LABELS.BuildingNumber },
    ],
  },
];

export function EtaSettingsForm({ onSaved }: { onSaved: () => void }) {
  const [form, setForm] = useState<EtaSettingsInput>(EMPTY);
  const [stored, setStored] = useState<EtaSettings | null>(null);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  useEffect(() => {
    api.getEtaSettings().then(
      (s) => {
        setStored(s);
        setForm({ ...s, clientSecret: null, presharedKey: null });
      },
      (e) => setMessage({ ok: false, text: errorMessage(e) }),
    );
  }, []);

  const set = (key: TextKey) => (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [key]: e.target.value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      const saved = await api.saveEtaSettings(form);
      setStored(saved);
      setForm({ ...saved, clientSecret: null, presharedKey: null });
      setMessage({ ok: true, text: "اتحفظت الإعدادات" });
      onSaved();
    } catch (e) {
      setMessage({ ok: false, text: errorMessage(e) });
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-5 rounded-xl border border-slate-800 bg-slate-900 p-5" aria-label="إعدادات الإيصال الإلكتروني">
      <div className="flex flex-wrap items-center gap-6">
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={form.enabled}
            onChange={(e) => setForm((f) => ({ ...f, enabled: e.target.checked }))}
            className="h-5 w-5 accent-emerald-500"
          />
          <span>تفعيل الإيصال الإلكتروني مع مصلحة الضرائب (اختياري)</span>
        </label>
        {form.enabled && (
          <label className="flex items-center gap-2">
            <span className="text-sm text-slate-400">البيئة</span>
            <select
              value={form.environment}
              onChange={(e) => setForm((f) => ({ ...f, environment: e.target.value as EtaSettingsInput["environment"] }))}
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
            >
              <option value="preProduction">تجريبية (Pre-production)</option>
              <option value="production">فعلية (Production)</option>
            </select>
          </label>
        )}
      </div>

      {!form.enabled && (
        <p className="text-sm text-slate-400" data-testid="eta-off-note">
          مقفول: البيع والإيصالات شغالين عادي، ومفيش أي حاجة بتتبعت للضرائب. لو فعّلته بعدين، الفواتير اللي هتتعمل من ساعتها بس هي اللي هتتبعت.
        </p>
      )}

      {form.enabled && SECTIONS.map((section) => (
        <fieldset key={section.title} className="space-y-3">
          <legend className="mb-2 font-bold text-slate-300">{section.title}</legend>
          <div className="grid gap-3 sm:grid-cols-2">
            {section.fields.map((f) => (
              <Field key={f.key} id={`eta-${f.key}`} label={f.label} value={form[f.key]} onChange={set(f.key)} dir={f.dir} />
            ))}
            {section.title.startsWith("بيانات الدخول") && (
              <>
                <Field
                  id="eta-clientSecret"
                  label={ETA_FIELD_LABELS.ClientSecret}
                  type="password"
                  value={form.clientSecret ?? ""}
                  placeholder={stored?.hasClientSecret ? "محفوظ (اكتب قيمة جديدة لتغييره)" : ""}
                  onChange={(e) => setForm((f) => ({ ...f, clientSecret: e.target.value }))}
                  dir="ltr"
                />
                <Field
                  id="eta-presharedKey"
                  label={ETA_FIELD_LABELS.PresharedKey}
                  type="password"
                  value={form.presharedKey ?? ""}
                  placeholder={stored?.hasPresharedKey ? "محفوظ (اكتب قيمة جديدة لتغييره)" : ""}
                  onChange={(e) => setForm((f) => ({ ...f, presharedKey: e.target.value }))}
                  dir="ltr"
                />
              </>
            )}
          </div>
        </fieldset>
      ))}

      <div className="flex items-center gap-4">
        <button type="submit" disabled={saving} className="rounded-lg bg-emerald-600 px-5 py-2 font-bold text-white hover:bg-emerald-500 disabled:bg-slate-700">
          {saving ? "جاري الحفظ…" : "حفظ الإعدادات"}
        </button>
        {message && (
          <span role={message.ok ? "status" : "alert"} className={message.ok ? "text-emerald-400" : "text-rose-400"}>
            {message.text}
          </span>
        )}
      </div>
    </form>
  );
}
