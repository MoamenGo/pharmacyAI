"use client";

import { useEffect, useState } from "react";
import { WRITE_OFF_REASONS } from "@/components/inventory/WriteOffForm";
import { api, type SalesReport } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { cairoToday, formatCairoDateTime, formatPiasters, formatQuantity } from "@/lib/format";

/** Sales between two dates: totals per day, best sellers, and stock written off in the period. */
export function SalesReportView({ onError }: { onError: (message: string | null) => void }) {
  const [today, setToday] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [report, setReport] = useState<SalesReport | null>(null);

  // Defaults to this month so far. Set after mount: the static export is prerendered at build time.
  useEffect(() => {
    const now = cairoToday();
    setToday(now);
    setFrom(`${now.slice(0, 8)}01`);
    setTo(now);
  }, []);

  useEffect(() => {
    if (!from || !to) return;
    let cancelled = false;
    api.getSalesReport(from, to).then(
      (r) => {
        if (cancelled) return;
        setReport(r);
        onError(null);
      },
      (e) => !cancelled && onError(errorMessage(e)),
    );
    return () => {
      cancelled = true;
    };
  }, [from, to, onError]);

  const days = report?.days.length ?? 0;
  const maxDay = Math.max(1, ...(report?.days.map((d) => d.totalPiasters) ?? [1]));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2">
          <span className="text-sm text-slate-400">من</span>
          <input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2" dir="ltr" />
        </label>
        <label className="flex items-center gap-2">
          <span className="text-sm text-slate-400">إلى</span>
          <input type="date" value={to} min={from || undefined} max={today || undefined} onChange={(e) => setTo(e.target.value)} className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2" dir="ltr" />
        </label>
      </div>

      {report && (
        <>
          <section className="grid gap-4 sm:grid-cols-3" data-testid="report-summary">
            <Stat label="إجمالي المبيعات" value={formatPiasters(report.totalPiasters)} />
            <Stat label="عدد الفواتير" value={String(report.count)} />
            <Stat label="متوسط الفاتورة" value={formatPiasters(report.count ? Math.round(report.totalPiasters / report.count) : 0)} />
          </section>

          <div className="grid gap-6 lg:grid-cols-2">
            <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <h2 className="mb-3 font-semibold">المبيعات يوم بيوم ({days} يوم فيه بيع)</h2>
              {days === 0 ? (
                <p className="text-sm text-slate-500">مفيش مبيعات في الفترة دي</p>
              ) : (
                <ul className="space-y-1 text-sm">
                  {report.days.map((d) => (
                    <li key={d.date} className="grid grid-cols-[6rem_1fr_7rem] items-center gap-3">
                      <span className="font-mono text-slate-400" dir="ltr">{d.date}</span>
                      <span className="h-3 rounded bg-emerald-700" style={{ width: `${(d.totalPiasters / maxDay) * 100}%` }} aria-hidden="true" />
                      <span className="text-left font-mono" dir="ltr">{formatPiasters(d.totalPiasters)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
              <h2 className="mb-3 font-semibold">الأكثر مبيعاً</h2>
              {report.topDrugs.length === 0 ? (
                <p className="text-sm text-slate-500">—</p>
              ) : (
                <ol className="space-y-1 text-sm">
                  {report.topDrugs.map((d, i) => (
                    <li key={d.drugId} className="flex justify-between gap-3">
                      <span>
                        <span className="ml-2 text-slate-500">{i + 1}.</span>
                        {d.drugNameAr}
                      </span>
                      <span className="font-mono" dir="ltr">{formatPiasters(d.totalPiasters)}</span>
                    </li>
                  ))}
                </ol>
              )}
            </section>
          </div>

          <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
            <h2 className="mb-3 font-semibold">الإعدام والتالف في الفترة</h2>
            {report.writeOffs.length === 0 ? (
              <p className="text-sm text-slate-500">مفيش</p>
            ) : (
              <table className="w-full text-right text-sm">
                <thead className="text-slate-400">
                  <tr>
                    <th className="py-1 font-medium">الصنف</th>
                    <th className="py-1 font-medium">التشغيلة</th>
                    <th className="py-1 font-medium">الكمية</th>
                    <th className="py-1 font-medium">السبب</th>
                    <th className="py-1 font-medium">الوقت</th>
                  </tr>
                </thead>
                <tbody>
                  {report.writeOffs.map((w) => (
                    <tr key={w.id} className="border-t border-slate-800">
                      <td className="py-1">{w.drugNameAr}</td>
                      <td className="py-1 font-mono" dir="ltr">{w.batchNumber}</td>
                      <td className="py-1">{formatQuantity(w.quantityInSubUnits, w.subUnitsPerPack, w.packUnitName, w.subUnitName)}</td>
                      <td className="py-1">
                        {WRITE_OFF_REASONS[w.reason]}
                        {w.note && <span className="text-slate-500"> · {w.note}</span>}
                      </td>
                      <td className="py-1 font-mono" dir="ltr">{formatCairoDateTime(w.createdAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
      <div className="text-sm text-slate-400">{label}</div>
      <div className="font-mono text-2xl font-bold text-emerald-400" dir="ltr">{value}</div>
    </div>
  );
}
