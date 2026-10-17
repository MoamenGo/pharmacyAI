"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { DrugPanel } from "@/components/inventory/DrugPanel";
import { DrugTable } from "@/components/inventory/DrugTable";
import { NewDrugDialog } from "@/components/inventory/NewDrugDialog";
import { ShortcutBar } from "@/components/ShortcutBar";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { api, type DrugDetails, type DrugSummary } from "@/lib/api";
import { errorMessage } from "@/lib/errors";

const SEARCH_DEBOUNCE_MS = 200;

// Scanner codes are letters and digits with at least one digit and no spaces (EAN, Code 128).
const looksLikeBarcode = (term: string) => /^[A-Za-z0-9-]{4,}$/.test(term) && /\d/.test(term);

export default function InventoryPage() {
  const [search, setSearch] = useState("");
  const [drugs, setDrugs] = useState<DrugSummary[]>([]);
  const [selected, setSelected] = useState<DrugDetails | null>(null);
  const [newDrugBarcode, setNewDrugBarcode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const receiveRef = useRef<HTMLInputElement>(null);
  // Only the latest request may update the list or the panel; older responses are dropped.
  const listSeq = useRef(0);
  const selectSeq = useRef(0);

  const refreshList = useCallback(async (term: string) => {
    const seq = ++listSeq.current;
    try {
      const result = await api.searchDrugs(term);
      if (seq !== listSeq.current) return;
      setDrugs(result);
      setError(null);
    } catch (e) {
      if (seq === listSeq.current) setError(errorMessage(e));
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => void refreshList(search), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search, refreshList]);

  useEffect(() => searchRef.current?.focus(), []);

  const select = async (id: string) => {
    const seq = ++selectSeq.current;
    try {
      const details = await api.getDrug(id);
      if (seq === selectSeq.current) setSelected(details);
    } catch (e) {
      if (seq === selectSeq.current) setError(errorMessage(e));
    }
  };

  const applyChange = (details: DrugDetails) => {
    setSelected(details);
    setDrugs((list) => {
      const exists = list.some((d) => d.id === details.drug.id);
      return exists ? list.map((d) => (d.id === details.drug.id ? details.drug : d)) : [details.drug, ...list];
    });
  };

  // A USB scanner types the barcode then presses Enter: open that drug, or offer to add it.
  const onSearchEnter = async () => {
    const term = search.trim();
    if (!term) return;
    const seq = ++selectSeq.current;
    try {
      const found = await api.getDrugByBarcode(term);
      if (seq !== selectSeq.current) return;
      if (found) {
        setSelected(found);
        receiveRef.current?.focus();
      } else if (looksLikeBarcode(term)) {
        setNewDrugBarcode(term);
      } else if (drugs.length === 0) {
        setError("مفيش صنف بالاسم أو الباركود ده");
      }
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  useFunctionKeys({
    F2: () => setNewDrugBarcode(""),
    F3: () => searchRef.current?.select(),
    F4: () => receiveRef.current?.focus(),
  });

  return (
    <main className="grid gap-6 p-6 pb-20 lg:grid-cols-[1fr_380px]">
      <section className="space-y-4">
        <input
          ref={searchRef}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && void onSearchEnter()}
          placeholder="ابحث بالاسم أو امسح الباركود…"
          className="w-full rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-lg"
          aria-label="بحث"
          autoComplete="off"
        />
        {error && <p role="alert" className="text-rose-400">{error}</p>}
        <DrugTable drugs={drugs} selectedId={selected?.drug.id ?? null} onSelect={(id) => void select(id)} />
      </section>

      {selected ? (
        <DrugPanel ref={receiveRef} details={selected} onChanged={applyChange} />
      ) : (
        <aside className="rounded-xl border border-dashed border-slate-800 p-8 text-center text-slate-500">اختار صنف لعرض التشغيلات واستلام كمية</aside>
      )}

      {newDrugBarcode !== null && (
        <NewDrugDialog
          initialBarcode={newDrugBarcode}
          onClose={() => {
            setNewDrugBarcode(null);
            searchRef.current?.focus();
          }}
          onCreated={(details) => {
            setNewDrugBarcode(null);
            applyChange(details);
          }}
        />
      )}

      <ShortcutBar
        shortcuts={[
          { key: "F2", label: "صنف جديد" },
          { key: "F3", label: "بحث" },
          { key: "F4", label: "استلام كمية" },
        ]}
      />
    </main>
  );
}
