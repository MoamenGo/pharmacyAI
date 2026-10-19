"use client";

import { useEffect, useRef } from "react";
import { AlternativesList } from "@/components/inventory/AlternativesList";
import type { DrugSummary } from "@/lib/api";

interface Props {
  drug: DrugSummary;
  onPick: (drugId: string) => void;
  onClose: () => void;
}

/** F7 at the till: same-ingredient drugs on the shelf, to offer when the asked-for one ran out. */
export function AlternativesDialog({ drug, onPick, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  return (
    <dialog
      ref={dialogRef}
      onClose={onClose}
      className="w-full max-w-xl rounded-xl border border-slate-700 bg-slate-900 p-6 text-slate-100 backdrop:bg-black/70"
    >
      <div className="space-y-4">
        <h2 className="text-xl font-bold">بدائل {drug.nameAr}</h2>
        {drug.activeIngredient && <p className="text-sm text-slate-400" dir="ltr">{drug.activeIngredient}</p>}
        <AlternativesList drug={drug} onOpen={onPick} stockedOnly />
        <button type="button" onClick={() => dialogRef.current?.close()} className="rounded-lg border border-slate-700 px-6 py-2">
          إغلاق <kbd className="mr-1 text-xs text-slate-500">Esc</kbd>
        </button>
      </div>
    </dialog>
  );
}
