import { ApiError } from "./api";

const MESSAGES: Record<string, string> = {
  "drug.barcode_taken": "يوجد صنف آخر بنفس الباركود",
  "stock.already_expired": "لا يمكن استلام دواء منتهي الصلاحية",
  "stock.loose_exceeds_pack": "عدد الشرائط الفرط لازم يكون أقل من علبة كاملة",
  "stock.insufficient": "الكمية المتاحة غير كافية",
  "stock.concurrent_update": "المخزون اتغير أثناء الحفظ، حاول مرة تانية",
};

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.code && MESSAGES[error.code]) return MESSAGES[error.code];
    if (error.status === 400) return "البيانات غير صحيحة، راجع الحقول";
    if (error.status === 404) return "الصنف غير موجود";
  }
  return "تعذر الاتصال بالخادم المحلي";
}
