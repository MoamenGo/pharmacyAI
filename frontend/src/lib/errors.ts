import { ApiError } from "./api";

const MESSAGES: Record<string, string> = {
  "drug.barcode_taken": "يوجد صنف آخر بنفس الباركود",
  "stock.already_expired": "لا يمكن استلام دواء منتهي الصلاحية",
  "stock.loose_exceeds_pack": "عدد الشرائط الفرط لازم يكون أقل من علبة كاملة",
  "stock.insufficient": "الكمية المتاحة غير كافية",
  "stock.concurrent_update": "المخزون اتغير أثناء الحفظ، حاول مرة تانية",
  "stock.quantity_too_large": "الكمية أكبر من المسموح للتشغيلة",
  "stock.batch_overdrawn": "الكمية أكبر من الموجود في التشغيلة",
  "stock.batch_not_found": "التشغيلة دي مش موجودة أو خلصت",
  "drug.invalid_price": "السعر لازم يكون أكبر من صفر",
  "drug.invalid_reorder_level": "حد الطلب لازم يكون صفر أو أكتر",
  "backup.invalid": "ملف النسخة ده بايظ ومينفعش يترجع",
  "sale.empty": "الفاتورة فاضية، ضيف صنف الأول",
  "sale.insufficient_payment": "المبلغ المدفوع أقل من الإجمالي",
  "sale.invalid_discount": "الخصم لازم يكون بين صفر وإجمالي الفاتورة",
  "sale.customer_required": "اختار العميل الأول عشان البيع الآجل",
  "sale.invalid_down_payment": "المدفوع دلوقتي لازم يكون بين صفر والإجمالي",
  "sale.invalid_card_reference": "رقم عملية الفيزا طويل أوي",
  "return.invalid_quantity": "الكمية أكبر من اللي لسه ممكن يترجع من الصنف ده",
  "return.no_lines": "اختار كمية لصنف واحد على الأقل",
  "return.customer_required": "المرتجع ده محتاج حساب العميل بتاع الفاتورة",
  "customer.phone_taken": "فيه عميل تاني بنفس رقم التليفون",
  "customer.invalid_name": "اكتب اسم العميل",
  "customer.overpayment": "المبلغ أكبر من اللي على العميل",
  "customer.invalid_amount": "المبلغ لازم يكون أكبر من صفر",
  "eta.invalid_rin": "رقم التسجيل الضريبي أرقام بس",
  "settings.reserved": "الإعداد ده ليه شاشته الخاصة",
  "outbox.already_sent": "الإيصال ده اتقبل خلاص",
  "request.invalid": "البيانات المرسلة غير صحيحة",
  "conflict.duplicate": "البيانات دي متسجلة قبل كده، حدّث الصفحة وحاول تاني",
};

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.code && MESSAGES[error.code]) return MESSAGES[error.code];
    if (error.status === 400) return "البيانات غير صحيحة، راجع الحقول";
    if (error.status === 404) return "الصنف غير موجود";
    if (error.status === 409) return "تعارض في البيانات، حدّث وحاول تاني";
    if (error.status >= 500) return "حصل خطأ في الخادم المحلي";
  }
  return "تعذر الاتصال بالخادم المحلي";
}
