"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const LINKS = [
  { href: "/", label: "الرئيسية" },
  { href: "/pos/", label: "البيع" },
  { href: "/inventory/", label: "المخزون" },
  { href: "/customers/", label: "العملاء" },
  { href: "/sync/", label: "المزامنة" },
];

export function NavBar() {
  const pathname = usePathname();

  return (
    <nav className="flex items-center gap-1 border-b border-slate-800 bg-slate-900 px-4">
      <span className="ml-6 py-3 text-lg font-bold text-emerald-400">PharmaAI</span>
      {LINKS.map((link) => {
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
