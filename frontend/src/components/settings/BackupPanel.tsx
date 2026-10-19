"use client";

import { useCallback, useEffect, useState } from "react";
import { api, type Backup } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatCairoDateTime } from "@/lib/format";

const formatSize = (bytes: number) => (bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

/** Backups of the database: one is taken automatically every day; take one now, download it, or go back to it. */
export function BackupPanel() {
  const [backups, setBackups] = useState<Backup[]>([]);
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState<string | null>(null);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      setBackups(await api.listBackups());
    } catch (e) {
      setMessage({ ok: false, text: errorMessage(e) });
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const run = async (action: () => Promise<string>) => {
    setBusy(true);
    setMessage(null);
    try {
      setMessage({ ok: true, text: await action() });
      await load();
    } catch (e) {
      setMessage({ ok: false, text: errorMessage(e) });
    } finally {
      setBusy(false);
      setConfirming(null);
    }
  };

  const backupNow = () => run(async () => `اتعملت نسخة ${(await api.createBackup()).name}`);

  const restore = (name: string) =>
    run(async () => {
      const result = await api.restoreBackup(name);
      return `رجعت البيانات لنسخة ${result.restored}. البيانات اللي كانت قبلها اتحفظت في ${result.safetyBackup} لو حبيت ترجعلها.`;
    });

  return (
    <div className="space-y-4 rounded-xl border border-slate-800 bg-slate-900 p-5" data-testid="backups">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-slate-400">
          البرنامج بيعمل نسخة لوحده كل يوم، وبيحتفظ بآخر 30 نسخة. نزّل نسخة على فلاشة من وقت للتاني عشان لو الجهاز نفسه باظ.
        </p>
        <button
          type="button"
          onClick={() => void backupNow()}
          disabled={busy}
          className="rounded-lg bg-emerald-600 px-4 py-2 font-semibold hover:bg-emerald-500 disabled:opacity-50"
        >
          اعمل نسخة دلوقتي
        </button>
      </div>

      {message && (
        <p role={message.ok ? "status" : "alert"} className={message.ok ? "text-emerald-400" : "text-rose-400"}>
          {message.text}
        </p>
      )}

      {backups.length === 0 ? (
        <p className="text-sm text-slate-500">لسه مفيش نسخ</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-slate-400">
            <tr>
              <th className="py-1 text-right font-normal">الوقت</th>
              <th className="py-1 text-right font-normal">الحجم</th>
              <th className="py-1" />
            </tr>
          </thead>
          <tbody>
            {backups.map((b) => (
              <tr key={b.name} className="border-t border-slate-800">
                <td className="py-2" dir="ltr">
                  {formatCairoDateTime(b.createdAtUtc)}
                </td>
                <td className="py-2" dir="ltr">
                  {formatSize(b.sizeBytes)}
                </td>
                <td className="flex justify-end gap-2 py-2">
                  <a href={api.backupUrl(b.name)} download={b.name} className="rounded-lg border border-slate-700 px-3 py-1 hover:border-emerald-500">
                    تنزيل
                  </a>
                  {confirming === b.name ? (
                    <button
                      type="button"
                      onClick={() => void restore(b.name)}
                      disabled={busy}
                      className="rounded-lg bg-rose-700 px-3 py-1 font-semibold hover:bg-rose-600 disabled:opacity-50"
                    >
                      متأكد؟ كل اللي اتعمل بعدها هيتشال
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={() => setConfirming(b.name)}
                      disabled={busy}
                      className="rounded-lg border border-slate-700 px-3 py-1 hover:border-rose-500"
                    >
                      استرجاع
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
