"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ShortcutBar } from "@/components/ShortcutBar";
import { CartTable } from "@/components/pos/CartTable";
import { PaymentPanel } from "@/components/pos/PaymentPanel";
import { ProductSearch } from "@/components/pos/ProductSearch";
import { Receipt } from "@/components/pos/Receipt";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { ApiError, api, type DailySales, type Sale } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters, parsePounds } from "@/lib/format";
import { lineTotal, shortDrugIds, useCartStore } from "@/stores/cartStore";

const PHARMACY_NAME_KEY = "pharmacy.name";
const CLEAR_CONFIRM_MS = 3000;

export default function PosPage() {
  const cart = useCartStore();
  const [paidText, setPaidText] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastSale, setLastSale] = useState<Sale | null>(null);
  const [today, setToday] = useState<DailySales | null>(null);
  const [pharmacyName, setPharmacyName] = useState<string | null>(null);
  const [confirmingClear, setConfirmingClear] = useState(false);
  const [printRequest, setPrintRequest] = useState(0);
  const searchRef = useRef<HTMLInputElement>(null);
  const quantityRef = useRef<HTMLInputElement>(null);
  const paidRef = useRef<HTMLInputElement>(null);

  const total = useMemo(() => cart.lines.reduce((sum, l) => sum + lineTotal(l), 0), [cart.lines]);
  const short = useMemo(() => shortDrugIds(cart.lines), [cart.lines]);
  const paid = paidText.trim() === "" ? null : parsePounds(paidText);

  const blockedReason =
    cart.lines.length === 0
      ? "الفاتورة فاضية، امسح أول صنف"
      : short.size > 0
        ? "فيه صنف الكمية المطلوبة منه أكبر من المتاح"
        : paidText.trim() !== "" && paid === null
          ? "اكتب المدفوع بالجنيه، مثلاً 100 أو 52.50"
          : paid !== null && paid < total
            ? "المدفوع أقل من الإجمالي"
            : null;

  const refreshToday = useCallback(async () => {
    try {
      setToday(await api.listSales());
    } catch {
      // The day's total is a convenience; the till keeps working without it.
    }
  }, []);

  useEffect(() => {
    searchRef.current?.focus();
    void refreshToday();
    api.getSetting(PHARMACY_NAME_KEY).then((s) => setPharmacyName(s?.value ?? null), () => {});
  }, [refreshToday]);

  useEffect(() => {
    if (!confirmingClear) return;
    const timer = setTimeout(() => setConfirmingClear(false), CLEAR_CONFIRM_MS);
    return () => clearTimeout(timer);
  }, [confirmingClear]);

  // Printing runs after the commit that put the receipt in the DOM, never before it.
  useEffect(() => {
    if (printRequest > 0) window.print();
  }, [printRequest]);

  const print = () => setPrintRequest((n) => n + 1);

  const focusSearch = () => searchRef.current?.focus();

  const submit = async () => {
    if (saving || blockedReason) return;
    setSaving(true);
    setError(null);
    try {
      const sale = await api.createSale({
        items: cart.lines.map((l) => ({ drugId: l.drug.id, unit: l.unit, quantity: l.quantity })),
        paidPiasters: paid ?? total,
      });
      setLastSale(sale);
      cart.clear();
      setPaidText("");
      print();
      void refreshToday();
      focusSearch();
    } catch (e) {
      setError(errorMessage(e));
      // Someone else sold this stock since it was scanned: show the real balance on the line.
      const drugId = e instanceof ApiError ? e.details.drugId : undefined;
      if (typeof drugId === "string") {
        api.getDrug(drugId).then((d) => cart.refreshDrug(d.drug), () => {});
      }
    } finally {
      setSaving(false);
    }
  };

  const newSale = () => {
    setLastSale(null);
    setError(null);
    setPaidText("");
    if (cart.lines.length > 0) {
      if (!confirmingClear) return setConfirmingClear(true);
      cart.clear();
    }
    setConfirmingClear(false);
    focusSearch();
  };

  const selected = cart.selectedKey;

  useFunctionKeys({
    F2: newSale,
    F3: () => searchRef.current?.select(),
    F4: () => selected && cart.toggleUnit(selected),
    F6: () => quantityRef.current?.focus(),
    F8: () => selected && cart.remove(selected),
    F9: () => paidRef.current?.focus(),
    F10: () => (lastSale && cart.lines.length === 0 ? print() : void submit()),
  });

  return (
    <main className="grid gap-6 p-6 pb-20 lg:grid-cols-[1fr_360px]">
      <section className="space-y-4">
        <div className="flex items-center justify-between text-sm text-slate-400">
          <h1 className="text-xl font-bold text-slate-100">فاتورة بيع</h1>
          {today && (
            <span data-testid="today-summary">
              مبيعات النهارده: {today.count} فاتورة ·{" "}
              <span className="font-mono" dir="ltr">{formatPiasters(today.totalPiasters)}</span>
            </span>
          )}
        </div>

        <ProductSearch
          ref={searchRef}
          onPick={(drug) => {
            setLastSale(null);
            setConfirmingClear(false);
            cart.add(drug);
          }}
          onNavigate={cart.moveSelection}
          onError={setError}
        />

        {confirmingClear && (
          <p role="status" className="rounded-lg bg-amber-950 px-4 py-2 text-amber-300">
            اضغط F2 تاني لمسح الفاتورة الحالية وبدء واحدة جديدة
          </p>
        )}

        {lastSale && (
          <div role="status" className="flex items-center justify-between rounded-xl border border-emerald-800 bg-emerald-950 px-4 py-3">
            <span>
              اتحفظت فاتورة رقم <b>{lastSale.number}</b> · الباقي للعميل{" "}
              <b className="font-mono" dir="ltr">{formatPiasters(lastSale.changePiasters)}</b>
            </span>
            <button type="button" onClick={print} className="rounded-lg border border-emerald-700 px-3 py-1 text-sm hover:bg-emerald-900">
              طباعة تاني (F10)
            </button>
          </div>
        )}

        <CartTable
          ref={quantityRef}
          lines={cart.lines}
          selectedKey={selected}
          shortDrugIds={short}
          onSelect={cart.select}
          onQuantity={cart.setQuantity}
          onToggleUnit={cart.toggleUnit}
          onRemove={cart.remove}
          onDone={focusSearch}
        />
      </section>

      <PaymentPanel
        ref={paidRef}
        totalPiasters={total}
        itemCount={cart.lines.length}
        paidText={paidText}
        paidPiasters={paid}
        onPaidText={setPaidText}
        blockedReason={blockedReason}
        saving={saving}
        error={error}
        onSubmit={() => void submit()}
      />

      {lastSale && <Receipt sale={lastSale} pharmacyName={pharmacyName} />}

      <ShortcutBar
        shortcuts={[
          { key: "F2", label: "فاتورة جديدة" },
          { key: "F3", label: "بحث" },
          { key: "F4", label: "علبة / شريط" },
          { key: "F6", label: "الكمية" },
          { key: "F8", label: "حذف الصنف" },
          { key: "F9", label: "المدفوع" },
          { key: "F10", label: "حفظ وطباعة" },
        ]}
      />
    </main>
  );
}
