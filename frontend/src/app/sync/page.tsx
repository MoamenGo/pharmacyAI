"use client";

import { useCallback, useEffect, useState } from "react";
import { ShortcutBar } from "@/components/ShortcutBar";
import { ETA_FIELD_LABELS, EtaSettingsForm } from "@/components/sync/EtaSettingsForm";
import { ProblemList } from "@/components/sync/ProblemList";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { api, type SyncStatus } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatCairoDateTime } from "@/lib/format";

const POLL_MS = 5000;
/** ETA refuses receipts issued more than 24 hours ago; warn well before that. */
const LATE_WARNING_HOURS = 12;

export default function SyncPage() {
  const [status, setStatus] = useState<SyncStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retrying, setRetrying] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);

  const refresh = useCallback(async () => {
    try {
      setStatus(await api.getSyncStatus());
      setError(null);
    } catch (e) {
      setError(errorMessage(e));
    }
  }, []);

  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), POLL_MS);
    return () => clearInterval(timer);
  }, [refresh]);

  const syncNow = async () => {
    if (syncing) return;
    setSyncing(true);
    try {
      await api.runSync();
      await refresh();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSyncing(false);
    }
  };

  const retry = async (id: string) => {
    setRetrying(id);
    try {
      await api.retrySyncMessage(id);
      await refresh();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setRetrying(null);
    }
  };

  useFunctionKeys({ F5: () => void (inUse ? syncNow() : refresh()) });

  const pendingHours = status?.oldestPendingAtUtc
    ? (Date.now() - Date.parse(status.oldestPendingAtUtc)) / 3_600_000
    : 0;
  // The link is optional: while it is off and nothing was ever queued, the queue screens are noise.
  const inUse = status !== null && (status.enabled || status.pending + status.sent + status.rejected > 0);

  return (
    <main className="mx-auto max-w-5xl space-y-6 p-6 pb-20">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">
          الربط مع مصلحة الضرائب (الإيصال الإلكتروني) <span className="text-base font-normal text-slate-400">· اختياري</span>
        </h1>
        {inUse && (
          <button
            type="button"
            onClick={() => void syncNow()}
            disabled={syncing}
            className="rounded-lg bg-emerald-600 px-4 py-2 font-semibold hover:bg-emerald-500 disabled:opacity-50"
          >
            زامن دلوقتي (F5)
          </button>
        )}
      </div>

      {error && (
        <p role="alert" className="rounded-lg bg-rose-950 px-4 py-2 text-rose-300">
          {error}
        </p>
      )}

      {status && !status.enabled && (
        <section role="status" className="space-y-1 rounded-xl border border-slate-800 bg-slate-900 px-5 py-4">
          <p className="font-semibold text-slate-200">الربط مقفول، والبرنامج شغال عادي من غيره.</p>
          <p className="text-sm text-slate-400">
            البيع والمخزون والتقارير مش محتاجين الربط. لو الصيدلية مسجلة في منظومة الإيصال الإلكتروني، علّم على &quot;إرسال الإيصالات&quot; تحت
            واملا البيانات. من ساعتها كل فاتورة جديدة هتتبعت تلقائياً؛ الفواتير اللي اتعملت والربط مقفول مش بتتبعت.
          </p>
        </section>
      )}

      {status && status.enabled && status.missingFields.length > 0 && (
        <p role="status" className="rounded-lg bg-amber-950 px-4 py-2 text-amber-300">
          ناقص بيانات قبل ما نقدر نبعت:{" "}
          {status.missingFields.map((f) => ETA_FIELD_LABELS[f] ?? f).join("، ")}
        </p>
      )}

      {status && status.pending > 0 && pendingHours >= LATE_WARNING_HOURS && (
        <p role="alert" className="rounded-lg bg-rose-950 px-4 py-2 text-rose-300">
          فيه إيصال مستني من {Math.floor(pendingHours)} ساعة. الضرائب بترفض أي إيصال عدّى عليه 24 ساعة، اتأكد من النت والإعدادات.
        </p>
      )}

      {status && inUse && (
        <section className="grid gap-4 sm:grid-cols-3" data-testid="sync-summary">
          <Stat label="مستني الإرسال" value={status.pending} tone="text-amber-300" />
          <Stat label="اتبعت" value={status.sent} tone="text-emerald-400" />
          <Stat label="مرفوض" value={status.rejected} tone="text-rose-400" />
          <p className="text-sm text-slate-400 sm:col-span-3">
            آخر إرسال ناجح:{" "}
            {status.lastSentAtUtc ? (
              <span dir="ltr" className="font-mono">{formatCairoDateTime(status.lastSentAtUtc)}</span>
            ) : (
              "لسه مفيش"
            )}
          </p>
        </section>
      )}

      {inUse && (
        <section className="space-y-3">
          <h2 className="text-lg font-semibold">إيصالات محتاجة انتباه</h2>
          <ProblemList problems={status?.problems ?? []} retrying={retrying} onRetry={(id) => void retry(id)} />
        </section>
      )}

      <EtaSettingsForm onSaved={() => void refresh()} />

      <ShortcutBar shortcuts={inUse ? [{ key: "F5", label: "زامن دلوقتي" }] : []} />
    </main>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
      <div className="text-sm text-slate-400">{label}</div>
      <div className={`font-mono text-3xl font-bold ${tone}`}>{value}</div>
    </div>
  );
}
