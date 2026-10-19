"use client";

import { useCallback, useEffect, useState } from "react";
import { ShortcutBar } from "@/components/ShortcutBar";
import { Receipt } from "@/components/pos/Receipt";
import { SalesReportView } from "@/components/sales/SalesReportView";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { api, type DailySales, type Sale } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { cairoToday, formatCairoDateTime, formatPiasters } from "@/lib/format";

const PHARMACY_NAME_KEY = "pharmacy.name";

/** One day's receipts (open one to see it and print it again) and a report over any date range. */
export default function SalesPage() {
  const [tab, setTab] = useState<"day" | "report">("day");
  const [date, setDate] = useState("");
  const [day, setDay] = useState<DailySales | null>(null);
  const [sale, setSale] = useState<Sale | null>(null);
  const [pharmacyName, setPharmacyName] = useState<string | null>(null);
  const [printRequest, setPrintRequest] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const loadDay = useCallback(async (d: string) => {
    try {
      setDay(await api.listSales(d));
      setError(null);
    } catch (e) {
      setError(errorMessage(e));
    }
  }, []);

  useEffect(() => {
    setSale(null);
    if (date) void loadDay(date);
  }, [date, loadDay]);

  useEffect(() => {
    setDate(cairoToday());
    api.getSetting(PHARMACY_NAME_KEY).then((s) => setPharmacyName(s?.value ?? null), () => {});
  }, []);

  useEffect(() => {
    if (printRequest > 0) window.print();
  }, [printRequest]);

  const open = async (id: string) => {
    try {
      setSale(await api.getSale(id));
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  useFunctionKeys({
    F10: () => sale && setPrintRequest((n) => n + 1),
  });

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-6 pb-20">
      <div className="flex items-center gap-2">
        <h1 className="ml-4 text-xl font-bold">المبيعات</h1>
        {(["day", "report"] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`rounded-lg px-4 py-2 ${tab === t ? "bg-emerald-700 text-white" : "border border-slate-700 text-slate-300 hover:border-emerald-600"}`}
          >
            {t === "day" ? "فواتير اليوم" : "تقرير فترة"}
          </button>
        ))}
      </div>

      {error && (
        <p role="alert" className="rounded-lg bg-rose-950 px-4 py-2 text-rose-300">
          {error}
        </p>
      )}

      {tab === "report" ? (
        <SalesReportView onError={setError} />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
          <section className="space-y-4">
            <div className="flex flex-wrap items-center gap-4">
              <label className="flex items-center gap-2">
                <span className="text-sm text-slate-400">اليوم</span>
                <input
                  type="date"
                  value={date}
                  max={date ? cairoToday() : undefined}
                  onChange={(e) => setDate(e.target.value)}
                  className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                  dir="ltr"
                />
              </label>
              {day && (
                <span className="text-slate-300" data-testid="day-summary">
                  {day.count} فاتورة · <span className="font-mono" dir="ltr">{formatPiasters(day.totalPiasters)}</span>
                </span>
              )}
            </div>

            {day && day.sales.length === 0 ? (
              <p className="rounded-xl border border-dashed border-slate-800 p-8 text-center text-slate-500">مفيش فواتير في اليوم ده</p>
            ) : (
              <table className="w-full overflow-hidden rounded-xl border border-slate-800 text-right">
                <thead className="bg-slate-900 text-sm text-slate-400">
                  <tr>
                    <th className="px-4 py-2 font-medium">رقم</th>
                    <th className="px-4 py-2 font-medium">الوقت</th>
                    <th className="px-4 py-2 font-medium">أصناف</th>
                    <th className="px-4 py-2 font-medium">الإجمالي</th>
                  </tr>
                </thead>
                <tbody>
                  {day?.sales.map((s) => (
                    <tr
                      key={s.id}
                      tabIndex={0}
                      onClick={() => void open(s.id)}
                      onKeyDown={(e) => e.key === "Enter" && void open(s.id)}
                      className={`cursor-pointer border-t border-slate-800 ${sale?.id === s.id ? "bg-emerald-950/50" : "hover:bg-slate-900"}`}
                    >
                      <td className="px-4 py-2 font-mono">{s.number}</td>
                      <td className="px-4 py-2 font-mono text-sm" dir="ltr">{formatCairoDateTime(s.soldAtUtc).split(" ")[1]}</td>
                      <td className="px-4 py-2">{s.lineCount}</td>
                      <td className="px-4 py-2 font-mono" dir="ltr">{formatPiasters(s.totalPiasters)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {sale ? (
            <aside className="space-y-3 rounded-xl border border-slate-800 bg-slate-900 p-4">
              <div className="flex items-center justify-between">
                <h2 className="text-lg font-bold">فاتورة رقم {sale.number}</h2>
                <button
                  type="button"
                  onClick={() => setPrintRequest((n) => n + 1)}
                  className="rounded-lg border border-emerald-700 px-3 py-1 text-sm hover:bg-emerald-900"
                >
                  طباعة (F10)
                </button>
              </div>
              <p className="text-sm text-slate-400" dir="ltr">{formatCairoDateTime(sale.soldAtUtc)}</p>
              <ul className="divide-y divide-slate-800 text-sm">
                {sale.lines.map((l) => (
                  <li key={l.lineNumber} className="py-2">
                    <div className="flex justify-between gap-2">
                      <span>{l.drugNameAr}</span>
                      <span className="font-mono" dir="ltr">{formatPiasters(l.lineTotalPiasters)}</span>
                    </div>
                    <div className="text-xs text-slate-500">
                      {l.quantity} {l.unitName} × <span dir="ltr">{formatPiasters(l.unitPricePiasters)}</span> · تشغيلة{" "}
                      <span dir="ltr" className="font-mono">{l.batches.map((b) => b.batchNumber).join(", ")}</span>
                    </div>
                  </li>
                ))}
              </ul>
              <dl className="grid grid-cols-2 gap-1 border-t border-slate-800 pt-2 text-sm">
                <dt className="text-slate-400">الإجمالي</dt>
                <dd className="font-mono font-bold" dir="ltr">{formatPiasters(sale.totalPiasters)}</dd>
                <dt className="text-slate-400">المدفوع</dt>
                <dd className="font-mono" dir="ltr">{formatPiasters(sale.paidPiasters)}</dd>
                <dt className="text-slate-400">الباقي</dt>
                <dd className="font-mono" dir="ltr">{formatPiasters(sale.changePiasters)}</dd>
              </dl>
            </aside>
          ) : (
            <aside className="rounded-xl border border-dashed border-slate-800 p-8 text-center text-slate-500">اختار فاتورة لعرضها وطباعتها تاني</aside>
          )}
        </div>
      )}

      {sale && <Receipt sale={sale} pharmacyName={pharmacyName} />}

      <ShortcutBar shortcuts={[{ key: "F10", label: "طباعة الفاتورة المختارة" }]} />
    </main>
  );
}
