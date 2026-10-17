import { expiryLevel, formatExpiry } from "@/lib/format";

const STYLES = {
  expired: "bg-rose-950 text-rose-300 border-rose-800",
  soon: "bg-amber-950 text-amber-300 border-amber-800",
  ok: "bg-slate-800 text-slate-300 border-slate-700",
};

const LABELS = { expired: "منتهي", soon: "قرب ينتهي", ok: null };

// `expired` comes from the server when known, since it decides expiry in Cairo time.
export function ExpiryBadge({ date, expired }: { date: string; expired?: boolean }) {
  const local = expiryLevel(date);
  const level = expired === undefined ? local : expired ? "expired" : local === "expired" ? "soon" : local;
  return (
    <span className={`inline-flex items-center gap-1 rounded border px-2 py-0.5 font-mono text-sm ${STYLES[level]}`} dir="ltr">
      {formatExpiry(date)}
      {LABELS[level] && <span dir="rtl" className="font-sans text-xs">{LABELS[level]}</span>}
    </span>
  );
}
