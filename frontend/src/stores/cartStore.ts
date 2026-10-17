import { create } from "zustand";
import type { DrugSummary, SaleUnit } from "@/lib/api";

export interface CartLine {
  key: string;
  drug: DrugSummary;
  unit: SaleUnit;
  quantity: number;
}

export const MAX_LINE_QUANTITY = 10_000;

export function unitPrice(line: CartLine): number {
  return line.unit === "pack" ? line.drug.publicPricePiasters : line.drug.subUnitPricePiasters;
}

export function lineTotal(line: CartLine): number {
  return unitPrice(line) * line.quantity;
}

export function subUnits(line: CartLine): number {
  return line.unit === "pack" ? line.quantity * line.drug.subUnitsPerPack : line.quantity;
}

/** Drug ids whose lines together ask for more than the stock we last saw. */
export function shortDrugIds(lines: CartLine[]): Set<string> {
  const wanted = new Map<string, number>();
  for (const line of lines) wanted.set(line.drug.id, (wanted.get(line.drug.id) ?? 0) + subUnits(line));
  const short = new Set<string>();
  for (const line of lines) {
    if ((wanted.get(line.drug.id) ?? 0) > line.drug.sellableQuantityInSubUnits) short.add(line.drug.id);
  }
  return short;
}

const keyOf = (drugId: string, unit: SaleUnit) => `${drugId}:${unit}`;

interface CartStore {
  lines: CartLine[];
  selectedKey: string | null;
  /** Adds one of the drug's packs, or one more if it is already on the sale. */
  add: (drug: DrugSummary, unit?: SaleUnit) => void;
  setQuantity: (key: string, quantity: number) => void;
  /** Switches a line between packs and loose sub-units, merging with an existing line of that unit. */
  toggleUnit: (key: string) => void;
  remove: (key: string) => void;
  select: (key: string | null) => void;
  moveSelection: (step: 1 | -1) => void;
  /** Replaces a drug's stock snapshot, e.g. after the server says it ran short. */
  refreshDrug: (drug: DrugSummary) => void;
  clear: () => void;
}

export const useCartStore = create<CartStore>((set) => ({
  lines: [],
  selectedKey: null,

  add: (drug, unit = "pack") =>
    set(({ lines }) => {
      const key = keyOf(drug.id, unit);
      const existing = lines.find((l) => l.key === key);
      const next = existing
        ? lines.map((l) => (l.key === key ? { ...l, drug, quantity: Math.min(l.quantity + 1, MAX_LINE_QUANTITY) } : l))
        : [...lines, { key, drug, unit, quantity: 1 }];
      return { lines: next, selectedKey: key };
    }),

  setQuantity: (key, quantity) =>
    set(({ lines }) => ({
      lines: lines.map((l) => (l.key === key ? { ...l, quantity: Math.max(1, Math.min(Math.trunc(quantity), MAX_LINE_QUANTITY)) } : l)),
    })),

  toggleUnit: (key) =>
    set(({ lines }) => {
      const line = lines.find((l) => l.key === key);
      if (!line || line.drug.subUnitsPerPack <= 1) return {};
      const unit: SaleUnit = line.unit === "pack" ? "subUnit" : "pack";
      const targetKey = keyOf(line.drug.id, unit);
      const target = lines.find((l) => l.key === targetKey);
      const next = target
        ? lines
            .filter((l) => l.key !== key)
            .map((l) => (l.key === targetKey ? { ...l, quantity: Math.min(l.quantity + line.quantity, MAX_LINE_QUANTITY) } : l))
        : lines.map((l) => (l.key === key ? { ...l, unit, key: targetKey } : l));
      return { lines: next, selectedKey: targetKey };
    }),

  remove: (key) =>
    set(({ lines, selectedKey }) => {
      const index = lines.findIndex((l) => l.key === key);
      const next = lines.filter((l) => l.key !== key);
      const fallback = next[Math.min(index, next.length - 1)]?.key ?? null;
      return { lines: next, selectedKey: selectedKey === key ? fallback : selectedKey };
    }),

  select: (selectedKey) => set({ selectedKey }),

  moveSelection: (step) =>
    set(({ lines, selectedKey }) => {
      if (lines.length === 0) return {};
      const index = lines.findIndex((l) => l.key === selectedKey);
      const nextIndex = index < 0 ? lines.length - 1 : Math.max(0, Math.min(lines.length - 1, index + step));
      return { selectedKey: lines[nextIndex].key };
    }),

  refreshDrug: (drug) => set(({ lines }) => ({ lines: lines.map((l) => (l.drug.id === drug.id ? { ...l, drug } : l)) })),

  clear: () => set({ lines: [], selectedKey: null }),
}));
