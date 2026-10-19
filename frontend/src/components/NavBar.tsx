"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";

const LINKS = [
  { href: "/", label: "الرئيسية" },
  { href: "/pos/", label: "البيع" },
  { href: "/inventory/", label: "المخزون" },
  { href: "/sales/", label: "المبيعات" },
  { href: "/customers/", label: "العملاء" },
  { href: "/sync/", label: "المزامنة", etaOnly: true },
  { href: "/settings/", label: "الإعدادات" },
];

const SETTINGS_CHANGED = "pharmaai:sync-settings-changed";

/** Lets the settings page show or hide the sync link right after the e-receipt is turned on or off. */
export function notifySyncSettingsChanged() {
  window.dispatchEvent(new Event(SETTINGS_CHANGED));
}

export function NavBar() {
  const pathname = usePathname();
  // The sync screen only matters once the e-receipt is on, or while receipts from before it was turned off still wait.
  const [showSync, setShowSync] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const load = () =>
      api.getSyncStatus().then(
        (s) => !cancelled && setShowSync(s.enabled || s.pending > 0 || s.rejected > 0),
        () => {
          // API not reachable yet: keep the current links.
        },
      );
    void load();
    window.addEventListener(SETTINGS_CHANGED, load);
    return () => {
      cancelled = true;
      window.removeEventListener(SETTINGS_CHANGED, load);
    };
  }, [pathname]);

  return (
    <nav className="flex items-center gap-1 border-b border-slate-800 bg-slate-900 px-4">
      <span className="ml-6 py-3 text-lg font-bold text-emerald-400">PharmaAI</span>
      {LINKS.filter((link) => !link.etaOnly || showSync || pathname.startsWith("/sync")).map((link) => {
        const active = pathname === link.href || (link.href !== "/" && pathname.startsWith(link.href.replace(/\/$/, "")));
        return (
          <Link
            key={link.href}
            href={link.href}
            className={`rounded-t-lg px-4 py-3 ${active ? "bg-slate-950 text-white" : "text-slate-400 hover:text-white"}`}
          >
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}
