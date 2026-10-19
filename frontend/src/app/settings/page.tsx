"use client";

import { EtaSettingsForm } from "@/components/sync/EtaSettingsForm";
import { notifySyncSettingsChanged } from "@/components/NavBar";

export default function SettingsPage() {
  return (
    <main className="mx-auto max-w-5xl space-y-6 p-6 pb-20">
      <h1 className="text-xl font-bold">الإعدادات</h1>

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">الإيصال الإلكتروني</h2>
        <EtaSettingsForm onSaved={notifySyncSettingsChanged} />
      </section>
    </main>
  );
}
