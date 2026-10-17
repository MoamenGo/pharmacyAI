"use client";

import { forwardRef, useEffect, useRef, useState } from "react";
import { api, type DrugSummary } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters, formatQuantity } from "@/lib/format";

const SEARCH_DEBOUNCE_MS = 150;
const MAX_RESULTS = 8;

interface Props {
  onPick: (drug: DrugSummary) => void;
  /** Arrow keys with the result list closed move the selection in the cart instead. */
  onNavigate: (step: 1 | -1) => void;
  onError: (message: string | null) => void;
}

/**
 * The cashier's one input: a USB scanner types a barcode and presses Enter, or the cashier
 * types part of a name and picks from the list with the arrow keys.
 */
export const ProductSearch = forwardRef<HTMLInputElement, Props>(function ProductSearch({ onPick, onNavigate, onError }, ref) {
  const [term, setTerm] = useState("");
  const [results, setResults] = useState<DrugSummary[]>([]);
  const [highlight, setHighlight] = useState(-1);
  const seq = useRef(0);

  useEffect(() => {
    const trimmed = term.trim();
    const current = ++seq.current;
    if (!trimmed) {
      setResults([]);
      return;
    }
    const timer = setTimeout(async () => {
      try {
        const found = await api.searchDrugs(trimmed);
        if (current !== seq.current) return;
        setResults(found.slice(0, MAX_RESULTS));
        setHighlight(-1);
      } catch (e) {
        if (current === seq.current) onError(errorMessage(e));
      }
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [term, onError]);

  const pick = (drug: DrugSummary) => {
    seq.current++;
    onPick(drug);
    onError(null);
    setTerm("");
    setResults([]);
    setHighlight(-1);
  };

  const onEnter = async () => {
    const trimmed = term.trim();
    if (!trimmed) return;
    if (highlight >= 0 && results[highlight]) return pick(results[highlight]);

    // Barcode first: a scan must never pick a different drug whose name happens to match.
    const current = ++seq.current;
    try {
      const found = await api.getDrugByBarcode(trimmed);
      if (current !== seq.current) return;
      if (found) return pick(found.drug);
      if (results.length === 1) return pick(results[0]);
      if (results.length > 1) return setHighlight(0);
      onError("مفيش صنف بالباركود أو الاسم ده");
      setTerm("");
    } catch (e) {
      onError(errorMessage(e));
    }
  };

  const onKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    const open = results.length > 0;
    if (event.key === "Enter") {
      event.preventDefault();
      void onEnter();
    } else if (event.key === "Escape" && open) {
      setResults([]);
    } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const step = event.key === "ArrowDown" ? 1 : -1;
      if (open) setHighlight((h) => Math.max(0, Math.min(results.length - 1, h + step)));
      else onNavigate(step);
    }
  };

  return (
    <div className="relative">
      <input
        ref={ref}
        value={term}
        onChange={(e) => setTerm(e.target.value)}
        onKeyDown={onKeyDown}
        placeholder="امسح الباركود أو اكتب اسم الصنف…"
        className="w-full rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-lg"
        aria-label="بحث عن صنف"
        aria-expanded={results.length > 0}
        aria-controls="pos-results"
        role="combobox"
        autoComplete="off"
      />
      {results.length > 0 && (
        <ul id="pos-results" role="listbox" className="absolute inset-x-0 top-full z-10 mt-1 overflow-hidden rounded-xl border border-slate-700 bg-slate-900 shadow-xl">
          {results.map((drug, i) => (
            <li
              key={drug.id}
              role="option"
              aria-selected={i === highlight}
              onMouseDown={(e) => {
                e.preventDefault();
                pick(drug);
              }}
              className={`flex cursor-pointer items-center justify-between gap-4 px-4 py-2 ${i === highlight ? "bg-emerald-900/60" : "hover:bg-slate-800"}`}
            >
              <span>
                {drug.nameAr}
                {drug.nameEn && <span className="mr-2 text-sm text-slate-500" dir="ltr">{drug.nameEn}</span>}
              </span>
              <span className="flex shrink-0 gap-4 text-sm">
                <span className={drug.sellableQuantityInSubUnits > 0 ? "text-slate-400" : "text-rose-400"}>
                  {drug.sellableQuantityInSubUnits > 0
                    ? formatQuantity(drug.sellableQuantityInSubUnits, drug.subUnitsPerPack, drug.packUnitName, drug.subUnitName)
                    : "نفد"}
                </span>
                <span className="font-mono" dir="ltr">{formatPiasters(drug.publicPricePiasters)}</span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
});
