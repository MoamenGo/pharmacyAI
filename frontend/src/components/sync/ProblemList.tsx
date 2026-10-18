"use client";

import type { SyncProblem } from "@/lib/api";
import { formatCairoDateTime } from "@/lib/format";

interface Props {
  problems: SyncProblem[];
  retrying: string | null;
  onRetry: (id: string) => void;
}

export function ProblemList({ problems, retrying, onRetry }: Props) {
  if (problems.length === 0) {
    return <p className="rounded-xl border border-dashed border-slate-800 p-6 text-center text-slate-500">مفيش إيصالات فيها مشاكل</p>;
  }

  return (
    <table className="w-full overflow-hidden rounded-xl border border-slate-800 text-right">
      <thead className="bg-slate-900 text-sm text-slate-400">
        <tr>
          <th className="px-3 py-2">فاتورة</th>
          <th className="px-3 py-2">الحالة</th>
          <th className="px-3 py-2">السبب</th>
          <th className="px-3 py-2">المحاولات</th>
          <th className="px-3 py-2"><span className="sr-only">إجراء</span></th>
        </tr>
      </thead>
      <tbody>
        {problems.map((p) => (
          <tr key={p.id} className="border-t border-slate-800 align-top">
            <td className="px-3 py-2 font-mono">{p.saleNumber}</td>
            <td className="whitespace-nowrap px-3 py-2">
              {p.status === "rejected" ? (
                <span className="text-rose-400">مرفوض</span>
              ) : (
                <span className="text-amber-300">
                  هيعيد المحاولة <span dir="ltr" className="font-mono text-xs">{formatCairoDateTime(p.nextAttemptAtUtc)}</span>
                </span>
              )}
            </td>
            <td className="px-3 py-2 text-sm text-slate-300" dir="auto">{p.lastError}</td>
            <td className="px-3 py-2 font-mono">{p.attempts}</td>
            <td className="px-3 py-2">
              <button
                type="button"
                onClick={() => onRetry(p.id)}
                disabled={retrying === p.id}
                className="whitespace-nowrap rounded-lg border border-slate-700 px-3 py-1 text-sm hover:border-emerald-500 disabled:opacity-50"
              >
                {p.status === "rejected" ? "تصحيح وإعادة إرسال" : "ابعت دلوقتي"}
              </button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
