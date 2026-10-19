"use client";

import { useEffect, useRef, useState } from "react";
import { ConnectionStatus } from "@/components/ConnectionStatus";
import { Dashboard } from "@/components/home/Dashboard";
import { PharmacyNameForm } from "@/components/PharmacyNameForm";
import { ShortcutBar } from "@/components/ShortcutBar";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { useConnectionStore } from "@/stores/connectionStore";

const STATUS_POLL_MS = 1000;
const STATUS_POLL_ATTEMPTS = 15;

export default function HomePage() {
  const check = useConnectionStore((s) => s.check);
  const online = useConnectionStore((s) => s.state.kind === "online");
  const nameInput = useRef<HTMLInputElement>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    // The sidecar API needs a moment to start: retry until it answers or we give up.
    let attempts = 0;
    let timer: ReturnType<typeof setTimeout>;

    const poll = async () => {
      await check();
      attempts += 1;
      if (useConnectionStore.getState().state.kind === "offline" && attempts < STATUS_POLL_ATTEMPTS) {
        timer = setTimeout(poll, STATUS_POLL_MS);
      }
    };

    poll();
    return () => clearTimeout(timer);
  }, [check]);

  useFunctionKeys({
    F2: () => nameInput.current?.focus(),
    F5: () => {
      void check();
      setRefreshKey((k) => k + 1);
    },
  });

  return (
    <main className="mx-auto max-w-5xl space-y-6 p-8 pb-20">
      <header>
        <h1 className="text-3xl font-bold">PharmaAI</h1>
        <p className="mt-1 text-slate-400">نظام إدارة الصيدليات، يعمل بدون إنترنت</p>
      </header>

      <ConnectionStatus />
      {online && <Dashboard refreshKey={refreshKey} />}
      <PharmacyNameForm ref={nameInput} />

      <ShortcutBar
        shortcuts={[
          { key: "F2", label: "اسم الصيدلية" },
          { key: "F5", label: "تحديث" },
        ]}
      />
    </main>
  );
}
