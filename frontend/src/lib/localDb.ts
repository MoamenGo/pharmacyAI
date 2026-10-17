import Dexie, { type EntityTable } from "dexie";

/** UI-side cache so the screen can still show the last known values while the API is starting. */
export interface CachedSetting {
  key: string;
  value: string;
  updatedAtUtc: string;
}

export const localDb = new Dexie("pharmaai-ui") as Dexie & {
  settings: EntityTable<CachedSetting, "key">;
};

localDb.version(1).stores({
  settings: "key",
});
