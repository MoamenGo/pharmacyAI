"use client";

import { useEffect, useRef, useState } from "react";
import { api, type CatalogDrug } from "@/lib/api";
import { formatPiasters } from "@/lib/format";

const SEARCH_DEBOUNCE_MS = 200;

interface Props {
  onPick: (drug: CatalogDrug) => void;
  autoFocus?: boolean;
}

/** Looks a product up in the bundled Egyptian drug list, by English or Arabic name or active ingredient. */
export function CatalogPicker({ onPick, autoFocus }: Props) {
  const [term, setTerm] = useState("");
  const [results, setResults] = useState<CatalogDrug[]>([]);
  const [highlight, setHighlight] = useState(0);
  const [failed, setFailed] = useState(false);
  const seq = useRef(0);

  useEffect(() => {
    const trimmed = term.trim();
    const current = ++seq.current;
    if (trimmed.length < 2) {
      setResults([]);
      return;
    }
    const timer = setTimeout(async () => {
      try {
        const found = await api.searchCatalog(trimmed);
        if (current !== seq.current) return;
        setResults(found);
        setHighlight(0);
        setFailed(false);
      } catch {
        if (current === seq.current) setFailed(true);
      }
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [term]);

  const pick = (drug: CatalogDrug) => {
    seq.current++;
    onPick(drug);
    setTerm("");
    setResults([]);
  };

  const onKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (results.length === 0) return;
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const step = event.key === "ArrowDown" ? 1 : -1;
      setHighlight((h) => Math.max(0, Math.min(results.length - 1, h + step)));
    } else if (event.key === "Enter") {
      event.preventDefault();
      pick(results[highlight]);
    } else if (event.key === "Escape") {
      // Close the list, not the dialog around it.
      event.preventDefault();
      setResults([]);
    }
  };

  return (
    <div className="relative">
      <label htmlFor="catalog-search" className="mb-1 block text-sm text-slate-400">
        ابحث في دليل الأدوية المصري (اختياري، بيملا البيانات لوحده)
      </label>
      <input
        id="catalog-search"
        value={term}
        onChange={(e) => setTerm(e.target.value)}
        onKeyDown={onKeyDown}
        placeholder="مثلاً: panadol أو بانادول أو paracetamol"
        className="w-full rounded-lg border border-emerald-800 bg-slate-950 px-3 py-2"
        role="combobox"
        aria-expanded={results.length > 0}
        aria-controls="catalog-results"
        autoComplete="off"
        autoFocus={autoFocus}
      />
      {failed && <p className="mt-1 text-sm text-rose-400">تعذر البحث في الدليل</p>}
      {results.length > 0 && (
        <ul
          id="catalog-results"
          role="listbox"
          className="absolute inset-x-0 top-full z-20 mt-1 max-h-80 overflow-y-auto rounded-xl border border-slate-700 bg-slate-900 shadow-xl"
        >
          {results.map((drug, i) => (
            <li
              key={drug.id}
              role="option"
              aria-selected={i === highlight}
              onMouseDown={(e) => {
                e.preventDefault();
                pick(drug);
              }}
              className={`cursor-pointer px-3 py-2 ${i === highlight ? "bg-emerald-900/60" : "hover:bg-slate-800"}`}
            >
              <div className="flex items-baseline justify-between gap-3">
                <span dir="ltr" className="font-semibold">{drug.nameEn}</span>
                {drug.pricePiasters !== null && (
                  <span className="shrink-0 font-mono text-sm text-slate-300" dir="ltr">{formatPiasters(drug.pricePiasters)}</span>
                )}
              </div>
              <div className="text-xs text-slate-500">
                {drug.nameAr}
                {drug.activeIngredient && <span dir="ltr"> · {drug.activeIngredient}</span>}
                {drug.manufacturer && <span dir="ltr"> · {drug.manufacturer}</span>}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Sensible pack and sub-unit names for a product's route; the pharmacist can still change them. */
export function unitsForRoute(route: string | null): { packUnitName: string; subUnitName: string } {
  switch (route) {
    case "ORAL.LIQUID":
    case "EYE":
    case "EAR":
    case "SPRAY":
    case "MOUTH":
      return { packUnitName: "زجاجة", subUnitName: "زجاجة" };
    case "INJECTION":
      return { packUnitName: "علبة", subUnitName: "أمبول" };
    case "TOPICAL":
      return { packUnitName: "أنبوبة", subUnitName: "أنبوبة" };
    case "EFF":
      return { packUnitName: "علبة", subUnitName: "كيس" };
    case "VAGINAL":
    case "RECTAL":
      return { packUnitName: "علبة", subUnitName: "لبوس" };
    default:
      return { packUnitName: "علبة", subUnitName: "شريط" };
  }
}
