"use client";

import { forwardRef, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { localDb } from "@/lib/localDb";

const KEY = "pharmacy.name";

type SaveState = { kind: "idle" } | { kind: "saving" } | { kind: "saved" } | { kind: "error"; message: string };

export const PharmacyNameForm = forwardRef<HTMLInputElement>(function PharmacyNameForm(_, inputRef) {
  const [value, setValue] = useState("");
  const [saveState, setSaveState] = useState<SaveState>({ kind: "idle" });

  useEffect(() => {
    let cancelled = false;

    (async () => {
      const cached = await localDb.settings.get(KEY);
      if (cached && !cancelled) setValue(cached.value);

      try {
        const remote = await api.getSetting(KEY);
        if (remote && !cancelled) {
          setValue(remote.value);
          await localDb.settings.put(remote);
        }
      } catch {
        // API not reachable yet: keep showing the cached value.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaveState({ kind: "saving" });
    try {
      const saved = await api.putSetting(KEY, value.trim());
      await localDb.settings.put(saved);
      setSaveState({ kind: "saved" });
    } catch (error) {
      const message =
        error instanceof ApiError && error.status === 400 ? "القيمة غير صالحة" : "تعذر الحفظ، تأكد من تشغيل الخادم المحلي";
      setSaveState({ kind: "error", message });
    }
  };

  return (
    <form onSubmit={save} className="rounded-xl border border-slate-800 bg-slate-900 p-5">
      <label htmlFor="pharmacy-name" className="mb-2 block text-sm text-slate-400">
        اسم الصيدلية
      </label>
      <div className="flex gap-3">
        <input
          id="pharmacy-name"
          ref={inputRef}
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            setSaveState({ kind: "idle" });
          }}
          className="flex-1 rounded-lg border border-slate-700 bg-slate-950 px-4 py-2 text-lg"
          autoComplete="off"
        />
        <button
          type="submit"
          disabled={saveState.kind === "saving"}
          className="rounded-lg bg-emerald-600 px-6 py-2 font-semibold hover:bg-emerald-500 disabled:opacity-50"
        >
          حفظ
        </button>
      </div>
      <p className="mt-2 h-5 text-sm" role="status">
        {saveState.kind === "saved" && <span className="text-emerald-400">تم الحفظ</span>}
        {saveState.kind === "error" && <span className="text-rose-400">{saveState.message}</span>}
      </p>
    </form>
  );
});
