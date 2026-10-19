"use client";

import { forwardRef, useEffect, useRef, useState } from "react";
import { api, type Customer } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { formatPiasters } from "@/lib/format";

interface Props {
  customer: Customer | null;
  onChange: (customer: Customer | null) => void;
}

const SEARCH_DELAY_MS = 250;
const MAX_RESULTS = 6;

/** Finds a customer by name or phone for a credit sale, or adds a new one on the spot. */
export const CustomerPicker = forwardRef<HTMLInputElement, Props>(function CustomerPicker({ customer, onChange }, ref) {
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<Customer[]>([]);
  const [adding, setAdding] = useState(false);
  const [phone, setPhone] = useState("");
  const [error, setError] = useState<string | null>(null);
  // Only the latest search may fill the list; slower, older answers are dropped.
  const searchSeq = useRef(0);

  useEffect(() => {
    if (customer || search.trim() === "") {
      setResults([]);
      return;
    }
    const seq = ++searchSeq.current;
    const timer = setTimeout(() => {
      api.listCustomers(search.trim()).then(
        (found) => seq === searchSeq.current && setResults(found.slice(0, MAX_RESULTS)),
        (e) => seq === searchSeq.current && setError(errorMessage(e)),
      );
    }, SEARCH_DELAY_MS);
    return () => clearTimeout(timer);
  }, [search, customer]);

  const pick = (picked: Customer) => {
    onChange(picked);
    setSearch("");
    setAdding(false);
    setError(null);
  };

  const add = async () => {
    try {
      pick(await api.createCustomer({ name: search.trim(), phone: phone.trim() || null }));
      setPhone("");
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  if (customer) {
    return (
      <div className="flex items-center justify-between rounded-lg border border-slate-700 bg-slate-950 px-3 py-2" data-testid="credit-customer">
        <div>
          <div className="font-semibold">{customer.name}</div>
          <div className="text-sm text-slate-400">
            عليه دلوقتي <span dir="ltr" className="font-mono">{formatPiasters(customer.balancePiasters)}</span>
          </div>
        </div>
        <button type="button" onClick={() => onChange(null)} className="rounded-lg border border-slate-700 px-2 py-1 text-sm hover:border-emerald-500">
          تغيير
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <input
        ref={ref}
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setError(null);
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter" && results[0]) {
            e.preventDefault();
            pick(results[0]);
          }
        }}
        placeholder="اسم العميل أو رقم تليفونه"
        aria-label="بحث عن عميل"
        autoComplete="off"
        className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
      />
      {results.length > 0 && (
        <ul className="divide-y divide-slate-800 rounded-lg border border-slate-800" role="listbox" aria-label="العملاء">
          {results.map((c) => (
            <li key={c.id}>
              <button type="button" role="option" aria-selected={false} onClick={() => pick(c)} className="flex w-full justify-between px-3 py-2 text-right hover:bg-slate-800">
                <span>
                  {c.name} {c.phone && <span className="font-mono text-xs text-slate-400" dir="ltr">{c.phone}</span>}
                </span>
                <span className="font-mono text-sm text-amber-300" dir="ltr">{formatPiasters(c.balancePiasters)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {search.trim() !== "" &&
        (adding ? (
          <div className="flex gap-2">
            <input
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="رقم التليفون (اختياري)"
              aria-label="تليفون العميل الجديد"
              inputMode="tel"
              dir="ltr"
              className="min-w-0 flex-1 rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
            />
            <button type="button" onClick={() => void add()} className="rounded-lg bg-emerald-700 px-3 py-2 text-sm font-semibold hover:bg-emerald-600">
              إضافة
            </button>
          </div>
        ) : (
          <button type="button" onClick={() => setAdding(true)} className="text-sm text-emerald-400 hover:underline">
            + عميل جديد باسم &quot;{search.trim()}&quot;
          </button>
        ))}
      {error && <p role="alert" className="text-sm text-rose-400">{error}</p>}
    </div>
  );
});
