export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://127.0.0.1:5197";

export interface SystemStatus {
  status: "ok" | "degraded";
  journalMode: string;
  serverTimeUtc: string;
}

export interface Setting {
  key: string;
  value: string;
  updatedAtUtc: string;
}

export interface DrugSummary {
  id: string;
  nameAr: string;
  nameEn: string | null;
  barcode: string;
  packUnitName: string;
  subUnitName: string;
  subUnitsPerPack: number;
  publicPricePiasters: number;
  subUnitPricePiasters: number;
  sellableQuantityInSubUnits: number;
  expiredQuantityInSubUnits: number;
  nearestExpiry: string | null;
  activeIngredient: string | null;
  manufacturer: string | null;
  reorderLevelInPacks: number;
  /** Sellable stock is below the reorder level. */
  isShort: boolean;
}

export interface StockBatch {
  id: string;
  batchNumber: string;
  expiryDate: string;
  quantityInSubUnits: number;
  isExpired: boolean;
  receivedAtUtc: string;
}

export interface DrugDetails {
  drug: DrugSummary;
  batches: StockBatch[];
}

export interface NewDrug {
  nameAr: string;
  nameEn: string | null;
  barcode: string;
  packUnitName: string;
  subUnitName: string;
  subUnitsPerPack: number;
  publicPricePiasters: number;
  activeIngredient: string | null;
  manufacturer: string | null;
  reorderLevelInPacks: number;
}

/** What can change after a drug is created; the pack size cannot. */
export interface DrugUpdate {
  nameAr: string;
  nameEn: string | null;
  barcode: string;
  publicPricePiasters: number;
  activeIngredient: string | null;
  manufacturer: string | null;
  reorderLevelInPacks: number;
}

export type WriteOffReason = "expired" | "damaged" | "lost" | "other";

export interface WriteOff {
  quantityInSubUnits: number;
  reason: WriteOffReason;
  note: string | null;
}

/** A product from the bundled Egyptian drug list; the price is the published one. */
export interface CatalogDrug {
  id: number;
  nameEn: string;
  nameAr: string;
  activeIngredient: string | null;
  manufacturer: string | null;
  drugClass: string | null;
  route: string | null;
  pricePiasters: number | null;
}

export interface Alternatives {
  activeIngredient: string | null;
  inStock: DrugSummary[];
  inMarket: CatalogDrug[];
}

export interface BatchAlert {
  drugId: string;
  drugNameAr: string;
  packUnitName: string;
  subUnitName: string;
  subUnitsPerPack: number;
  batchId: string;
  batchNumber: string;
  expiryDate: string;
  quantityInSubUnits: number;
}

export interface InventoryAlerts {
  today: string;
  shortages: DrugSummary[];
  expiringSoon: BatchAlert[];
  expired: BatchAlert[];
}

export interface SalesReport {
  from: string;
  to: string;
  count: number;
  totalPiasters: number;
  days: { date: string; count: number; totalPiasters: number }[];
  topDrugs: { drugId: string; drugNameAr: string; quantityInSubUnits: number; totalPiasters: number }[];
  writeOffs: {
    id: string;
    drugId: string;
    drugNameAr: string;
    batchNumber: string;
    expiryDate: string;
    quantityInSubUnits: number;
    packUnitName: string;
    subUnitName: string;
    subUnitsPerPack: number;
    reason: WriteOffReason;
    note: string | null;
    createdAtUtc: string;
  }[];
}

export interface StockReceipt {
  batchNumber: string;
  expiryDate: string;
  packs: number;
  looseSubUnits: number;
}

export type SaleUnit = "pack" | "subUnit";

export interface NewSale {
  items: { drugId: string; unit: SaleUnit; quantity: number }[];
  paidPiasters: number;
}

export interface SaleLine {
  lineNumber: number;
  drugId: string;
  drugNameAr: string;
  unit: SaleUnit;
  unitName: string;
  quantity: number;
  quantityInSubUnits: number;
  unitPricePiasters: number;
  lineTotalPiasters: number;
  batches: { batchNumber: string; expiryDate: string; quantityInSubUnits: number }[];
}

export interface Sale {
  id: string;
  number: number;
  soldAtUtc: string;
  businessDate: string;
  totalPiasters: number;
  paidPiasters: number;
  changePiasters: number;
  lines: SaleLine[];
}

export interface DailySales {
  date: string;
  count: number;
  totalPiasters: number;
  sales: { id: string; number: number; soldAtUtc: string; lineCount: number; totalPiasters: number }[];
}

export type EtaEnvironment = "preProduction" | "production";

export interface EtaSettingsFields {
  enabled: boolean;
  environment: EtaEnvironment;
  clientId: string;
  posSerial: string;
  posOsVersion: string;
  posModelFramework: string;
  rin: string;
  companyTradeName: string;
  branchCode: string;
  governate: string;
  regionCity: string;
  street: string;
  buildingNumber: string;
  activityCode: string;
}

/** As read back: the two secrets only say whether they are stored. */
export interface EtaSettings extends EtaSettingsFields {
  hasClientSecret: boolean;
  hasPresharedKey: boolean;
  missingFields: string[];
}

/** As saved: an empty secret keeps the stored one. */
export interface EtaSettingsInput extends EtaSettingsFields {
  clientSecret: string | null;
  presharedKey: string | null;
}

export type OutboxStatus = "pending" | "sent" | "rejected";

export interface SyncProblem {
  id: string;
  saleNumber: number;
  status: OutboxStatus;
  attempts: number;
  lastError: string | null;
  createdAtUtc: string;
  nextAttemptAtUtc: string;
}

export interface SyncStatus {
  enabled: boolean;
  missingFields: string[];
  pending: number;
  sent: number;
  rejected: number;
  oldestPendingAtUtc: string | null;
  lastSentAtUtc: string | null;
  problems: SyncProblem[];
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errors: Record<string, string[]> = {},
    readonly code: string | null = null,
    /** Extra problem fields, e.g. drugId and availableInSubUnits on stock.insufficient. */
    readonly details: Record<string, unknown> = {},
  ) {
    super(`API request failed with status ${status}`);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (!response.ok) {
    const body = response.headers.get("content-type")?.includes("json") ? await response.json() : {};
    throw new ApiError(response.status, body.errors ?? {}, body.code ?? null, body);
  }

  // 202/204 carry no body.
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  getStatus: () => request<SystemStatus>("/api/health"),

  getSetting: async (key: string): Promise<Setting | null> => {
    try {
      return await request<Setting>(`/api/settings/${encodeURIComponent(key)}`);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },

  putSetting: (key: string, value: string) =>
    request<Setting>(`/api/settings/${encodeURIComponent(key)}`, {
      method: "PUT",
      body: JSON.stringify({ value }),
    }),

  searchDrugs: (search: string) => request<DrugSummary[]>(`/api/drugs?search=${encodeURIComponent(search)}`),

  getDrug: (id: string) => request<DrugDetails>(`/api/drugs/${id}`),

  getDrugByBarcode: async (barcode: string): Promise<DrugDetails | null> => {
    try {
      return await request<DrugDetails>(`/api/drugs/by-barcode/${encodeURIComponent(barcode)}`);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },

  createDrug: (drug: NewDrug) => request<DrugDetails>("/api/drugs", { method: "POST", body: JSON.stringify(drug) }),

  receiveStock: (drugId: string, receipt: StockReceipt) =>
    request<DrugDetails>(`/api/drugs/${drugId}/batches`, { method: "POST", body: JSON.stringify(receipt) }),

  updateDrug: (drugId: string, drug: DrugUpdate) =>
    request<DrugDetails>(`/api/drugs/${drugId}`, { method: "PUT", body: JSON.stringify(drug) }),

  writeOff: (drugId: string, batchId: string, writeOff: WriteOff) =>
    request<DrugDetails>(`/api/drugs/${drugId}/batches/${batchId}/write-offs`, { method: "POST", body: JSON.stringify(writeOff) }),

  getAlternatives: (drugId: string) => request<Alternatives>(`/api/drugs/${drugId}/alternatives`),

  getInventoryAlerts: () => request<InventoryAlerts>("/api/inventory/alerts"),

  searchCatalog: (search: string, limit = 12) =>
    request<CatalogDrug[]>(`/api/catalog?search=${encodeURIComponent(search)}&limit=${limit}`),

  createSale: (sale: NewSale) => request<Sale>("/api/sales", { method: "POST", body: JSON.stringify(sale) }),

  listSales: (date?: string) => request<DailySales>(`/api/sales${date ? `?date=${date}` : ""}`),

  getSale: (id: string) => request<Sale>(`/api/sales/${id}`),

  getSalesReport: (from: string, to: string) => request<SalesReport>(`/api/reports/sales?from=${from}&to=${to}`),

  getSyncStatus: () => request<SyncStatus>("/api/sync/status"),

  runSync: () => request<void>("/api/sync/run", { method: "POST" }),

  retrySyncMessage: (id: string) => request<void>(`/api/sync/messages/${id}/retry`, { method: "POST" }),

  getEtaSettings: () => request<EtaSettings>("/api/sync/eta-settings"),

  saveEtaSettings: (settings: EtaSettingsInput) =>
    request<EtaSettings>("/api/sync/eta-settings", { method: "PUT", body: JSON.stringify(settings) }),
};
