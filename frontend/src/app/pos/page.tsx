"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ShortcutBar } from "@/components/ShortcutBar";
import { AlternativesDialog } from "@/components/pos/AlternativesDialog";
import { CartTable } from "@/components/pos/CartTable";
import { PAYMENT_METHODS, PaymentPanel } from "@/components/pos/PaymentPanel";
import { ProductSearch } from "@/components/pos/ProductSearch";
import { Receipt } from "@/components/pos/Receipt";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { ApiError, api, type Customer, type DailySales, type PaymentMethod, type Sale } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters, parseDiscount, parsePounds } from "@/lib/format";
import { lineTotal, shortDrugIds, useCartStore } from "@/stores/cartStore";

const PHARMACY_NAME_KEY = "pharmacy.name";
const CLEAR_CONFIRM_MS = 3000;

export default function PosPage() {
  const cart = useCartStore();
  const [paidText, setPaidText] = useState("");
  const [method, setMethod] = useState<PaymentMethod>("cash");
  const [discountText, setDiscountText] = useState("");
  const [cardReference, setCardReference] = useState("");
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showAlternatives, setShowAlternatives] = useState(false);
  const [lastSale, setLastSale] = useState<Sale | null>(null);
  const [today, setToday] = useState<DailySales | null>(null);
  const [pharmacyName, setPharmacyName] = useState<string | null>(null);
  const [confirmingClear, setConfirmingClear] = useState(false);
  const [printRequest, setPrintRequest] = useState(0);
  const searchRef = useRef<HTMLInputElement>(null);
  const quantityRef = useRef<HTMLInputElement>(null);
  const paidRef = useRef<HTMLInputElement>(null);
  const discountRef = useRef<HTMLInputElement>(null);
  const customerRef = useRef<HTMLInputElement>(null);

  const subtotal = useMemo(() => cart.lines.reduce((sum, l) => sum + lineTotal(l), 0), [cart.lines]);
  const short = useMemo(() => shortDrugIds(cart.lines), [cart.lines]);
  const discount = parseDiscount(discountText, subtotal);
  const total = subtotal - (discount ?? 0);
  const paid = paidText.trim() === "" ? null : parsePounds(paidText);

  const blockedReason =
    cart.lines.length === 0
      ? "الفاتورة فاضية، امسح أول صنف"
      : short.size > 0
        ? "فيه صنف الكمية المطلوبة منه أكبر من المتاح"
        : discount === null
          ? "اكتب الخصم بالجنيه أو بالنسبة، مثلاً 5 أو 10%"
          : discount > subtotal
            ? "الخصم أكبر من إجمالي الفاتورة"
            : method !== "card" && paidText.trim() !== "" && paid === null
              ? "اكتب المدفوع بالجنيه، مثلاً 100 أو 52.50"
              : method === "cash" && paid !== null && paid < total
                ? "المدفوع أقل من الإجمالي"
                : method === "credit" && !customer
                  ? "اختار العميل للبيع الآجل"
                  : method === "credit" && paid !== null && paid > total
                    ? "المدفوع دلوقتي أكبر من الإجمالي"
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

  const resetPayment = () => {
    setPaidText("");
    setMethod("cash");
    setDiscountText("");
    setCardReference("");
    setCustomer(null);
  };

  const chooseMethod = (next: PaymentMethod) => {
    setMethod(next);
    setPaidText("");
    setError(null);
    // The customer search only exists once the panel re-renders for credit.
    setTimeout(() => (next === "credit" && !customer ? customerRef.current : paidRef.current)?.focus(), 0);
  };

  const cycleMethod = () => {
    const index = PAYMENT_METHODS.findIndex((m) => m.value === method);
    chooseMethod(PAYMENT_METHODS[(index + 1) % PAYMENT_METHODS.length].value);
  };

  const submit = async () => {
    if (saving || blockedReason) return;
    setSaving(true);
    setError(null);
    try {
      const sale = await api.createSale({
        items: cart.lines.map((l) => ({ drugId: l.drug.id, unit: l.unit, quantity: l.quantity })),
        paidPiasters: method === "cash" ? (paid ?? total) : method === "credit" ? (paid ?? 0) : total,
        paymentMethod: method,
        discountPiasters: discount ?? 0,
        customerId: method === "credit" ? (customer?.id ?? null) : null,
        cardReference: method === "card" && cardReference.trim() !== "" ? cardReference.trim() : null,
      });
      setLastSale(sale);
      cart.clear();
      resetPayment();
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
    if (cart.lines.length > 0) {
      if (!confirmingClear) return setConfirmingClear(true);
      cart.clear();
    }
    resetPayment();
    setConfirmingClear(false);
    focusSearch();
  };

  const selected = cart.selectedKey;
  const selectedDrug = cart.lines.find((l) => l.key === selected)?.drug ?? null;

  const addAlternative = async (drugId: string) => {
    setShowAlternatives(false);
    try {
      cart.add((await api.getDrug(drugId)).drug);
    } catch (e) {
      setError(errorMessage(e));
    }
    focusSearch();
  };

  useFunctionKeys({
    F2: newSale,
    F3: () => searchRef.current?.select(),
    F4: () => selected && cart.toggleUnit(selected),
    F5: cycleMethod,
    F6: () => quantityRef.current?.focus(),
    F7: () => discountRef.current?.select(),
    F8: () => selected && cart.remove(selected),
    F9: () => (method === "credit" && !customer ? customerRef.current : paidRef.current)?.focus(),
    F10: () => (lastSale && cart.lines.length === 0 ? print() : void submit()),
    F11: () => selectedDrug && setShowAlternatives(true),
  });

  return (
    <main className="grid gap-6 p-6 pb-20 lg:grid-cols-[1fr_360px]">
      {showAlternatives && selectedDrug && (
        <AlternativesDialog
          drug={selectedDrug}
          onPick={(id) => void addAlternative(id)}
          onClose={() => {
            setShowAlternatives(false);
            focusSearch();
          }}
        />
      )}

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
              اتحفظت فاتورة رقم <b>{lastSale.number}</b> ·{" "}
              {lastSale.paymentMethod === "credit" ? (
                <>
                  على حساب {lastSale.customerName}{" "}
                  <b className="font-mono" dir="ltr">{formatPiasters(lastSale.duePiasters)}</b>
                </>
              ) : lastSale.paymentMethod === "card" ? (
                "اتدفعت بالفيزا"
              ) : (
                <>
                  الباقي للعميل <b className="font-mono" dir="ltr">{formatPiasters(lastSale.changePiasters)}</b>
                </>
              )}
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
        refs={{ paid: paidRef, discount: discountRef, customer: customerRef }}
        subtotalPiasters={subtotal}
        discountPiasters={discount}
        totalPiasters={total}
        itemCount={cart.lines.length}
        method={method}
        onMethod={chooseMethod}
        discountText={discountText}
        onDiscountText={setDiscountText}
        paidText={paidText}
        paidPiasters={paid}
        onPaidText={setPaidText}
        cardReference={cardReference}
        onCardReference={setCardReference}
        customer={customer}
        onCustomer={(c) => {
          setCustomer(c);
          if (c) setTimeout(() => paidRef.current?.focus(), 0);
        }}
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
          { key: "F5", label: "طريقة الدفع" },
          { key: "F6", label: "الكمية" },
          { key: "F7", label: "خصم" },
          { key: "F8", label: "حذف الصنف" },
          { key: "F9", label: "المدفوع" },
          { key: "F10", label: "حفظ وطباعة" },
          { key: "F11", label: "البدائل" },
        ]}
      />
    </main>
  );
}
