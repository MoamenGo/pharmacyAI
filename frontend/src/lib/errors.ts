import { ApiError } from "./api";

const MESSAGES: Record<string, string> = {
  "drug.barcode_taken": "يوجد صنف آخر بنفس الباركود",
  "stock.already_expired": "لا يمكن استلام دواء منتهي الصلاحية",
  "stock.loose_exceeds_pack": "عدد الشرائط الفرط لازم يكون أقل من علبة كاملة",
  "stock.insufficient": "الكمية المتاحة غير كافية",
  "stock.concurrent_update": "المخزون اتغير أثناء الحفظ، حاول مرة تانية",
  "stock.quantity_too_large": "الكمية أكبر من المسموح للتشغيلة",
  "sale.empty": "الفاتورة فاضية، ضيف صنف الأول",
  "sale.insufficient_payment": "المبلغ المدفوع أقل من الإجمالي",
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
