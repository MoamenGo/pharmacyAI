"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ExpiryBadge } from "@/components/inventory/ExpiryBadge";
import { api, type BatchAlert, type DailySales, type InventoryAlerts } from "@/lib/api";
import { formatPiasters, formatQuantity } from "@/lib/format";

const MAX_ROWS = 8;

/** Today's sales and what needs the pharmacist's attention, each linked to the drug in the inventory screen. */
export function Dashboard({ refreshKey }: { refreshKey: number }) {
  const [today, setToday] = useState<DailySales | null>(null);
  const [alerts, setAlerts] = useState<InventoryAlerts | null>(null);

  useEffect(() => {
    let cancelled = false;
    api.listSales().then((s) => !cancelled && setToday(s), () => {});
    api.getInventoryAlerts().then((a) => !cancelled && setAlerts(a), () => {});
    return () => {
      cancelled = true;
    };
  }, [refreshKey]);

  return (
    <div className="space-y-4">
      <section className="grid gap-4 sm:grid-cols-4" data-testid="dashboard-stats">
        <Stat label="مبيعات النهارده" value={today ? formatPiasters(today.totalPiasters) : "…"} sub={today ? `${today.count} فاتورة` : ""} tone="text-emerald-400" href="/sales/" />
        <Stat label="نواقص" value={alerts ? String(alerts.shortages.length) : "…"} sub="تحت حد الطلب" tone="text-amber-300" />
        <Stat label="قرب ينتهي" value={alerts ? String(alerts.expiringSoon.length) : "…"} sub="خلال 3 شهور" tone="text-amber-300" />
        <Stat label="منتهي على الرف" value={alerts ? String(alerts.expired.length) : "…"} sub="محتاج إعدام" tone="text-rose-400" />
      </section>

      {alerts && alerts.shortages.length > 0 && (
        <Panel title="نواقص محتاجة طلب" count={alerts.shortages.length}>
          {alerts.shortages.slice(0, MAX_ROWS).map((d) => (
            <Row key={d.id} drugId={d.id} name={d.nameAr}>
              <span className={d.sellableQuantityInSubUnits === 0 ? "text-rose-400" : "text-amber-300"}>
                {d.sellableQuantityInSubUnits === 0
                  ? "نفد"
                  : formatQuantity(d.sellableQuantityInSubUnits, d.subUnitsPerPack, d.packUnitName, d.subUnitName)}
              </span>
              <span className="text-slate-500">
                الحد {d.reorderLevelInPacks} {d.packUnitName}
              </span>
            </Row>
          ))}
        </Panel>
      )}

      {alerts && alerts.expired.length > 0 && (
        <Panel title="أدوية منتهية لسه في المخزون" count={alerts.expired.length}>
          {alerts.expired.slice(0, MAX_ROWS).map((b) => <BatchRow key={b.batchId} batch={b} expired />)}
        </Panel>
      )}

      {alerts && alerts.expiringSoon.length > 0 && (
        <Panel title="صلاحيتها قربت تخلص" count={alerts.expiringSoon.length}>
          {alerts.expiringSoon.slice(0, MAX_ROWS).map((b) => <BatchRow key={b.batchId} batch={b} />)}
        </Panel>
      )}

      {alerts && alerts.shortages.length + alerts.expired.length + alerts.expiringSoon.length === 0 && (
        <p className="rounded-xl border border-dashed border-slate-800 p-6 text-center text-slate-500">
          مفيش تنبيهات. حدد &quot;حد الطلب&quot; لكل صنف من شاشة المخزون عشان النواقص تظهر هنا.
        </p>
      )}
    </div>
  );
}

function Stat({ label, value, sub, tone, href }: { label: string; value: string; sub: string; tone: string; href?: string }) {
  const body = (
    <>
      <div className="text-sm text-slate-400">{label}</div>
      <div className={`font-mono text-2xl font-bold ${tone}`} dir="ltr">{value}</div>
      <div className="text-xs text-slate-500">{sub}</div>
    </>
  );
  return href ? (
    <Link href={href} className="rounded-xl border border-slate-800 bg-slate-900 p-4 hover:border-emerald-700">
      {body}
    </Link>
  ) : (
    <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">{body}</div>
  );
}

function Panel({ title, count, children }: { title: string; count: number; children: React.ReactNode }) {
  return (
    <section className="rounded-xl border border-slate-800 bg-slate-900 p-4">
      <h2 className="mb-2 font-semibold">
        {title} <span className="text-sm text-slate-500">({count})</span>
      </h2>
      <ul className="divide-y divide-slate-800">{children}</ul>
      {count > MAX_ROWS && (
        <Link href="/inventory/" className="mt-2 block text-sm text-emerald-400 hover:underline">
          و{count - MAX_ROWS} كمان، افتح المخزون
        </Link>
      )}
    </section>
  );
}

function Row({ drugId, name, children }: { drugId: string; name: string; children: React.ReactNode }) {
  return (
    <li>
      <Link href={`/inventory/?drug=${drugId}`} className="flex items-center justify-between gap-3 py-2 text-sm hover:text-emerald-300">
        <span className="font-semibold">{name}</span>
        <span className="flex shrink-0 items-center gap-3">{children}</span>
      </Link>
    </li>
  );
}

function BatchRow({ batch, expired }: { batch: BatchAlert; expired?: boolean }) {
  return (
    <Row drugId={batch.drugId} name={batch.drugNameAr}>
      <span className="font-mono text-slate-500" dir="ltr">{batch.batchNumber}</span>
      <ExpiryBadge date={batch.expiryDate} expired={expired} />
      <span>{formatQuantity(batch.quantityInSubUnits, batch.subUnitsPerPack, batch.packUnitName, batch.subUnitName)}</span>
    </Row>
  );
}
