"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ShortcutBar } from "@/components/ShortcutBar";
import { CustomerAccount } from "@/components/customers/CustomerAccount";
import { useFunctionKeys } from "@/hooks/useFunctionKeys";
import { api, type Customer, type CustomerAccount as Account } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters } from "@/lib/format";

const SEARCH_DEBOUNCE_MS = 200;

export default function CustomersPage() {
  const [search, setSearch] = useState("");
  const [owingOnly, setOwingOnly] = useState(false);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [account, setAccount] = useState<Account | null>(null);
  const [newName, setNewName] = useState("");
  const [newPhone, setNewPhone] = useState("");
  const [error, setError] = useState<string | null>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const nameRef = useRef<HTMLInputElement>(null);
  const amountRef = useRef<HTMLInputElement>(null);
  // Only the latest request may update the list or the account; older responses are dropped.
  const listSeq = useRef(0);
  const selectSeq = useRef(0);

  const refreshList = useCallback(async (term: string, owing: boolean) => {
    const seq = ++listSeq.current;
    try {
      const result = await api.listCustomers(term, owing);
      if (seq !== listSeq.current) return;
      setCustomers(result);
      setError(null);
    } catch (e) {
      if (seq === listSeq.current) setError(errorMessage(e));
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => void refreshList(search, owingOnly), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search, owingOnly, refreshList]);

  useEffect(() => searchRef.current?.focus(), []);

  const select = async (id: string) => {
    const seq = ++selectSeq.current;
    try {
      const details = await api.getCustomer(id);
      if (seq === selectSeq.current) setAccount(details);
    } catch (e) {
      if (seq === selectSeq.current) setError(errorMessage(e));
    }
  };

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    if (newName.trim() === "") return;
    try {
      const customer = await api.createCustomer({ name: newName.trim(), phone: newPhone.trim() || null });
      setNewName("");
      setNewPhone("");
      void refreshList(search, owingOnly);
      await select(customer.id);
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  useFunctionKeys({
    F2: () => nameRef.current?.focus(),
    F3: () => searchRef.current?.select(),
    F9: () => amountRef.current?.focus(),
  });

  return (
    <main className="grid gap-6 p-6 pb-20 lg:grid-cols-[420px_1fr]">
      <section className="space-y-4">
        <h1 className="text-xl font-bold">العملاء والحسابات الآجلة</h1>

        <input
          ref={searchRef}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="ابحث بالاسم أو التليفون (F3)"
          aria-label="بحث عن عميل"
          autoComplete="off"
          className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
        />
        <label className="flex items-center gap-2 text-sm text-slate-300">
          <input type="checkbox" checked={owingOnly} onChange={(e) => setOwingOnly(e.target.checked)} className="h-4 w-4 accent-emerald-500" />
          اللي عليهم فلوس بس
        </label>

        {error && <p role="alert" className="text-rose-400">{error}</p>}

        <ul className="divide-y divide-slate-800 rounded-xl border border-slate-800" aria-label="قائمة العملاء">
          {customers.length === 0 && <li className="p-6 text-center text-slate-500">مفيش عملاء</li>}
          {customers.map((c) => (
            <li key={c.id}>
              <button
                type="button"
                onClick={() => void select(c.id)}
                className={`flex w-full items-center justify-between px-4 py-3 text-right hover:bg-slate-900 ${account?.customer.id === c.id ? "bg-slate-900" : ""}`}
              >
                <span>
                  {c.name}
                  {c.phone && <span className="mr-2 font-mono text-xs text-slate-400" dir="ltr">{c.phone}</span>}
                </span>
                <span className={`font-mono ${c.balancePiasters > 0 ? "text-amber-300" : "text-slate-500"}`} dir="ltr">
                  {formatPiasters(c.balancePiasters)}
                </span>
              </button>
            </li>
          ))}
        </ul>

        <form onSubmit={create} className="space-y-2 rounded-xl border border-slate-800 bg-slate-900 p-4" aria-label="عميل جديد">
          <h2 className="font-semibold">عميل جديد (F2)</h2>
          <input
            ref={nameRef}
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="الاسم"
            aria-label="اسم العميل"
            maxLength={100}
            autoComplete="off"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
          />
          <input
            value={newPhone}
            onChange={(e) => setNewPhone(e.target.value)}
            placeholder="التليفون (اختياري)"
            aria-label="تليفون العميل"
            inputMode="tel"
            maxLength={20}
            autoComplete="off"
            dir="ltr"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
          />
          <button
            type="submit"
            disabled={newName.trim() === ""}
            className="w-full rounded-lg bg-emerald-700 py-2 font-semibold hover:bg-emerald-600 disabled:bg-slate-700 disabled:text-slate-400"
          >
            إضافة العميل
          </button>
        </form>
      </section>

      {account ? (
        <CustomerAccount
          ref={amountRef}
          account={account}
          onPaid={(customer) => {
            setCustomers((list) => list.map((c) => (c.id === customer.id ? customer : c)));
            void select(customer.id);
          }}
        />
      ) : (
        <p className="self-start rounded-xl border border-dashed border-slate-800 p-10 text-center text-slate-500">
          اختار عميل عشان تشوف حسابه وتسجل دفعة
        </p>
      )}

      <ShortcutBar
        shortcuts={[
          { key: "F2", label: "عميل جديد" },
          { key: "F3", label: "بحث" },
          { key: "F9", label: "تسجيل دفعة" },
        ]}
      />
    </main>
  );
}
