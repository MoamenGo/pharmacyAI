"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ShortcutBar } from "@/components/ShortcutBar";
import { SaleDetails, methodLabel, refundMessage } from "@/components/sales/SaleDetails";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { api, type DailySales, type Sale } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatCairoDateTime, formatPiasters } from "@/lib/format";

function cairoToday(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Cairo" }).format(new Date());
}

export default function SalesPage() {
  const [date, setDate] = useState(cairoToday);
  const [day, setDay] = useState<DailySales | null>(null);
  const [selected, setSelected] = useState<Sale | null>(null);
  const [number, setNumber] = useState("");
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const numberRef = useRef<HTMLInputElement>(null);
  const returnRef = useRef<HTMLButtonElement>(null);

  const loadDay = useCallback(async () => {
    try {
      setDay(await api.listSales(date));
      setError(null);
    } catch (e) {
      setError(errorMessage(e));
    }
  }, [date]);

  useEffect(() => {
    void loadDay();
  }, [loadDay]);

  const open = async (load: () => Promise<Sale | null>) => {
    setNotice(null);
    try {
      const sale = await load();
      if (sale) {
        setSelected(sale);
        setError(null);
      } else {
        setError("مفيش فاتورة بالرقم ده");
      }
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  const findByNumber = (event: React.FormEvent) => {
    event.preventDefault();
    const n = Number(number);
    if (Number.isInteger(n) && n > 0) void open(() => api.getSaleByNumber(n));
  };

  useFunctionKeys({
    F3: () => numberRef.current?.select(),
    F9: () => returnRef.current?.click(),
  });

  const drawer = day ? day.cashPiasters + day.collectedPiasters - day.cashRefundsPiasters : 0;

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-6 pb-20">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h1 className="text-xl font-bold">المبيعات والمرتجعات</h1>
        <div className="flex flex-wrap items-end gap-3">
          <form onSubmit={findByNumber} className="flex items-end gap-2">
            <label className="space-y-1">
              <span className="block text-sm text-slate-400">رقم الفاتورة (F3)</span>
              <input
                ref={numberRef}
                value={number}
                onChange={(e) => setNumber(e.target.value)}
                inputMode="numeric"
                dir="ltr"
                className="w-32 rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 focus:border-emerald-500 focus:outline-none"
              />
            </label>
            <button type="submit" className="rounded-lg border border-slate-700 px-4 py-2 hover:border-emerald-500">
              افتح
            </button>
          </form>
          <label className="space-y-1">
            <span className="block text-sm text-slate-400">اليوم</span>
            <input
              type="date"
              value={date}
              max={cairoToday()}
              onChange={(e) => e.target.value && setDate(e.target.value)}
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
            />
          </label>
        </div>
      </div>

      {error && (
        <p role="alert" className="rounded-lg bg-rose-950 px-4 py-2 text-rose-300">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="rounded-lg bg-emerald-950 px-4 py-2 text-emerald-300">
          {notice}
        </p>
      )}

      {day && (
        <section className="grid gap-3 sm:grid-cols-4" data-testid="day-summary">
          <Stat label={`${day.count} فاتورة`} value={day.totalPiasters} />
          <Stat label="كاش" value={day.cashPiasters} />
          <Stat label="فيزا" value={day.cardPiasters} />
          <Stat label="آجل" value={day.creditPiasters} />
          <Stat label="تحصيل من العملاء" value={day.collectedPiasters} />
          <Stat label={`مرتجعات (${day.returnsCount})`} value={day.returnsPiasters} tone="text-amber-300" />
          <Stat label="كاش اترجع" value={day.cashRefundsPiasters} tone="text-amber-300" />
          <Stat label="المفروض في الدرج" value={drawer} tone="text-emerald-400" testId="drawer" />
        </section>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="overflow-hidden rounded-xl border border-slate-800">
          <table className="w-full text-sm">
            <thead className="bg-slate-900 text-slate-400">
              <tr>
                <th className="px-3 py-2 text-right font-normal">رقم</th>
                <th className="px-3 py-2 text-right font-normal">الوقت</th>
                <th className="px-3 py-2 text-right font-normal">الدفع</th>
                <th className="px-3 py-2 text-right font-normal">الإجمالي</th>
              </tr>
            </thead>
            <tbody>
              {day?.sales.map((s) => (
                <tr
                  key={s.id}
                  onClick={() => void open(() => api.getSale(s.id))}
                  className={`cursor-pointer border-t border-slate-800 hover:bg-slate-900 ${selected?.id === s.id ? "bg-slate-900" : ""}`}
                >
                  <td className="px-3 py-2 font-mono">{s.number}</td>
                  <td className="px-3 py-2" dir="ltr">
                    {formatCairoDateTime(s.soldAtUtc).slice(-5)}
                  </td>
                  <td className="px-3 py-2">
                    {methodLabel(s.paymentMethod)}
                    {s.customerName && ` · ${s.customerName}`}
                  </td>
                  <td className="px-3 py-2 font-mono" dir="ltr">
                    {formatPiasters(s.totalPiasters)}
                  </td>
                </tr>
              ))}
              {day && day.sales.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-3 py-6 text-center text-slate-500">
                    مفيش فواتير في اليوم ده
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </section>

        {selected ? (
          <SaleDetails
            ref={returnRef}
            sale={selected}
            onReturned={(sale, saved) => {
              setSelected(sale);
              setNotice(refundMessage(saved));
              void loadDay();
            }}
          />
        ) : (
          <p className="rounded-xl border border-dashed border-slate-800 p-6 text-slate-500">
            اختار فاتورة أو اكتب رقمها عشان تشوفها أو ترجّع منها
          </p>
        )}
      </div>

      <ShortcutBar
        shortcuts={[
          { key: "F3", label: "رقم الفاتورة" },
          { key: "F9", label: "ترجيع" },
        ]}
      />
    </main>
  );
}

function Stat({ label, value, tone = "text-slate-100", testId }: { label: string; value: number; tone?: string; testId?: string }) {
  return (
    <div className="rounded-xl border border-slate-800 bg-slate-900 p-3" data-testid={testId}>
      <div className="text-sm text-slate-400">{label}</div>
      <div className={`font-mono text-xl font-bold ${tone}`} dir="ltr">
        {formatPiasters(value)}
      </div>
    </div>
  );
}
