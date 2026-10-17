import { create } from "zustand";
import { api, type SystemStatus } from "@/lib/api";

type ConnectionState =
  | { kind: "checking" }
  | { kind: "online"; status: SystemStatus; checkedAt: Date }
  | { kind: "offline"; checkedAt: Date };

interface ConnectionStore {
  state: ConnectionState;
  check: () => Promise<void>;
}

export const useConnectionStore = create<ConnectionStore>((set) => ({
  state: { kind: "checking" },
  check: async () => {
    set({ state: { kind: "checking" } });
    try {
      const status = await api.getStatus();
      set({ state: { kind: "online", status, checkedAt: new Date() } });
    } catch {
      set({ state: { kind: "offline", checkedAt: new Date() } });
    }
  },
}));
