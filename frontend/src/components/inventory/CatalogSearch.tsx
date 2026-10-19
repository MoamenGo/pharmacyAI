"use client";

import { forwardRef, useEffect, useRef, useState } from "react";
import { api, type CatalogDrug } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters } from "@/lib/format";

interface Props {
  onPick: (drug: CatalogDrug) => void;
}

const SEARCH_DELAY_MS = 200;

/** Looks a product up in the bundled Egyptian drug list by trade name or active ingredient. */
export const CatalogSearch = forwardRef<HTMLInputElement, Props>(function CatalogSearch({ onPick }, ref) {
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<CatalogDrug[]>([]);
  const [highlight, setHighlight] = useState(0);
  const [error, setError] = useState<string | null>(null);
  // Only the latest search may fill the list; slower, older answers are dropped.
  const searchSeq = useRef(0);

  useEffect(() => {
    const term = search.trim();
    if (term.length < 2) {
      setResults([]);
      return;
    }
    const seq = ++searchSeq.current;
    const timer = setTimeout(() => {
      api.searchCatalog(term).then(
        (found) => {
          if (seq !== searchSeq.current) return;
          setResults(found);
          setHighlight(0);
        },
        (e) => seq === searchSeq.current && setError(errorMessage(e)),
      );
    }, SEARCH_DELAY_MS);
    return () => clearTimeout(timer);
  }, [search]);

  const pick = (drug: CatalogDrug) => {
    onPick(drug);
    setSearch("");
    setResults([]);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (results.length === 0) return;
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      const step = e.key === "ArrowDown" ? 1 : -1;
      setHighlight((h) => (h + step + results.length) % results.length);
    } else if (e.key === "Enter") {
      // Inside the new-drug form: Enter picks the highlighted product instead of submitting.
      e.preventDefault();
      pick(results[highlight]);
    }
  };

  return (
    <div className="space-y-2">
      <label htmlFor="catalogSearch" className="block text-sm text-slate-400">
        دوّر في دليل الأدوية المصري (بالاسم العربي أو الإنجليزي أو المادة الفعالة)
      </label>
      <input
        id="catalogSearch"
        ref={ref}
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setError(null);
        }}
        onKeyDown={onKeyDown}
        autoComplete="off"
        autoFocus
        placeholder="مثلاً: بانادول أو augmentin"
        className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 focus:border-emerald-500 focus:outline-none"
      />
      {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}
      {results.length > 0 && (
        <ul role="listbox" aria-label="نتايج دليل الأدوية" className="max-h-64 overflow-y-auto rounded-lg border border-slate-700 bg-slate-950">
          {results.map((drug, i) => (
            <li
              key={`${drug.nameEn}-${i}`}
              role="option"
              aria-selected={i === highlight}
              onMouseDown={(e) => {
                e.preventDefault();
                pick(drug);
              }}
              onMouseEnter={() => setHighlight(i)}
              className={`cursor-pointer px-3 py-2 ${i === highlight ? "bg-emerald-900/50" : ""}`}
            >
              <div className="flex items-baseline justify-between gap-3">
                <span className="font-semibold">{drug.nameAr || drug.nameEn}</span>
                <span dir="ltr" className="font-mono text-emerald-400">{formatPiasters(drug.publicPricePiasters)}</span>
              </div>
              <div dir="ltr" className="text-right text-xs text-slate-400">
                {drug.nameEn} · {drug.scientificName}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
});
