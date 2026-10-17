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

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errors: Record<string, string[]> = {},
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
    throw new ApiError(response.status, body.errors ?? {});
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
};
