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
}

export interface StockReceipt {
  batchNumber: string;
  expiryDate: string;
  packs: number;
  looseSubUnits: number;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errors: Record<string, string[]> = {},
    readonly code: string | null = null,
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
    throw new ApiError(response.status, body.errors ?? {}, body.code ?? null);
  }

  return (await response.json()) as T;
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
};
