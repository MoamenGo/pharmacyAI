"use client";

import { useConnectionStore } from "@/stores/connectionStore";

export function ConnectionStatus() {
  const state = useConnectionStore((s) => s.state);

  const view = {
    checking: { dot: "bg-amber-400 animate-pulse", label: "جاري الاتصال بالخادم المحلي…", detail: null },
    online:
      state.kind === "online" && state.status.status === "ok"
        ? { dot: "bg-emerald-400", label: "متصل بالخادم المحلي", detail: `قاعدة البيانات: ${state.status.journalMode.toUpperCase()}` }
        : { dot: "bg-amber-400", label: "متصل، لكن قاعدة البيانات ليست في وضع WAL", detail: null },
    offline: { dot: "bg-rose-500", label: "الخادم المحلي لا يستجيب", detail: "اضغط F5 لإعادة المحاولة" },
  }[state.kind];

  return (
    <section aria-live="polite" className="rounded-xl border border-slate-800 bg-slate-900 p-5">
      <div className="flex items-center gap-3">
        <span className={`h-3 w-3 rounded-full ${view.dot}`} />
        <p className="text-lg font-semibold">{view.label}</p>
      </div>
      {view.detail && <p className="mt-2 text-sm text-slate-400">{view.detail}</p>}
    </section>
  );
}
