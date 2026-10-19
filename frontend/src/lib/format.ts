/** Formats whole piasters as EGP without ever touching floating point. */
export function formatPiasters(piasters: number): string {
  const sign = piasters < 0 ? "-" : "";
  const abs = Math.abs(piasters);
  const pounds = Math.trunc(abs / 100);
  const rest = String(abs % 100).padStart(2, "0");
  return `${sign}${pounds}.${rest} ج.م`;
}

/** Parses "12.5" / "12.50" / "12" into piasters; returns null for anything else. */
export function parsePounds(input: string): number | null {
  const match = /^\s*(\d{1,7})(?:[.,](\d{1,2}))?\s*$/.exec(input);
  if (!match) return null;
  const pounds = Number(match[1]);
  const piasters = Number((match[2] ?? "0").padEnd(2, "0"));
  return pounds * 100 + piasters;
}

/**
 * A receipt discount typed as pounds ("5", "2.50") or a percentage of the subtotal ("10%").
 * Empty means no discount; returns null when the text is neither.
 */
export function parseDiscount(input: string, subtotalPiasters: number): number | null {
  const text = input.trim();
  if (text === "") return 0;
  const percent = /^(\d{1,3}(?:[.,]\d{1,2})?)\s*%$/.exec(text);
  if (percent) {
    const rate = Number(percent[1].replace(",", "."));
    return rate > 100 ? null : Math.round((subtotalPiasters * rate) / 100);
  }
  return parsePounds(text);
}

/** "2 علبة + 1 شريط" from a sub-unit count. */
export function formatQuantity(subUnits: number, perPack: number, packName: string, subName: string): string {
  if (perPack <= 1) return `${subUnits} ${packName}`;
  const packs = Math.trunc(subUnits / perPack);
  const loose = subUnits % perPack;
  const parts = [];
  if (packs > 0) parts.push(`${packs} ${packName}`);
  if (loose > 0 || packs === 0) parts.push(`${loose} ${subName}`);
  return parts.join(" + ");
}

/** "2027-06-30" → "06/2027" (pharmacies read expiry as month/year). */
export function formatExpiry(isoDate: string): string {
  const [year, month] = isoDate.split("-");
  return `${month}/${year}`;
}

/** "2027-06" (from an <input type="month">) → "2027-06-30", the last sellable day of that month. */
export function endOfMonth(yearMonth: string): string {
  const [year, month] = yearMonth.split("-").map(Number);
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return `${year}-${String(month).padStart(2, "0")}-${String(lastDay).padStart(2, "0")}`;
}

export type ExpiryLevel = "expired" | "soon" | "ok";

export const EXPIRY_WARNING_DAYS = 90;

export function expiryLevel(isoDate: string, today = new Date()): ExpiryLevel {
  const todayIso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
  if (isoDate < todayIso) return "expired";
  const days = (Date.parse(isoDate) - Date.parse(todayIso)) / 86_400_000;
  return days <= EXPIRY_WARNING_DAYS ? "soon" : "ok";
}

/** "17/10/2026 12:00" in Cairo time, with Latin digits, for receipts. */
export function formatCairoDateTime(isoUtc: string): string {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Africa/Cairo",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).formatToParts(new Date(isoUtc));
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)?.value ?? "";
  return `${get("day")}/${get("month")}/${get("year")} ${get("hour")}:${get("minute")}`;
}

/** Piasters as a plain amount for an input box: 4550 → "45.50", 4500 → "45". */
export function formatPounds(piasters: number): string {
  const pounds = Math.trunc(piasters / 100);
  const rest = piasters % 100;
  return rest === 0 ? String(pounds) : `${pounds}.${String(rest).padStart(2, "0")}`;
}

/** Today's date in Cairo as "2026-10-18", for date inputs and API queries. */
export function cairoToday(now = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Cairo", year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}
