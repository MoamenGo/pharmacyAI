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
