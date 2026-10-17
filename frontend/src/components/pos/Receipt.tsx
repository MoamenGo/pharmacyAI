"use client";

import { createPortal } from "react-dom";
import type { Sale } from "@/lib/api";
import { formatCairoDateTime, formatPiasters } from "@/lib/format";

/**
 * The paper receipt. It is rendered straight into <body> and hidden on screen; the print
 * stylesheet hides everything else, so window.print() prints only this, sized for 80 mm rolls.
 * Only rendered after a sale is saved, so it never runs during the static prerender.
 */
export function Receipt({ sale, pharmacyName }: { sale: Sale; pharmacyName: string | null }) {
  return createPortal(
    <section className="receipt" dir="rtl" aria-hidden="true">
      <h1 className="receipt-title">{pharmacyName || "صيدلية"}</h1>
      <div className="receipt-row">
        <span>فاتورة رقم {sale.number}</span>
        <span dir="ltr">{formatCairoDateTime(sale.soldAtUtc)}</span>
      </div>
      <hr />
      <table>
        <tbody>
          {sale.lines.map((line) => (
            <tr key={line.lineNumber}>
              <td>
                {line.drugNameAr}
                <div className="receipt-muted">
                  {line.quantity} {line.unitName} × <span dir="ltr">{formatPiasters(line.unitPricePiasters)}</span>
                </div>
              </td>
              <td className="receipt-amount" dir="ltr">{formatPiasters(line.lineTotalPiasters)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <hr />
      <div className="receipt-row receipt-total">
        <span>الإجمالي</span>
        <span dir="ltr">{formatPiasters(sale.totalPiasters)}</span>
      </div>
      <div className="receipt-row">
        <span>المدفوع</span>
        <span dir="ltr">{formatPiasters(sale.paidPiasters)}</span>
      </div>
      <div className="receipt-row">
        <span>الباقي</span>
        <span dir="ltr">{formatPiasters(sale.changePiasters)}</span>
      </div>
      <hr />
      <p className="receipt-footer">شكراً لزيارتكم، مع تمنياتنا بالشفاء</p>
    </section>,
    document.body,
  );
}
