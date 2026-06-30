import type { Habit, HabitCompletion } from "../types";

// Web shim — expo-sqlite's WASM worker doesn't resolve under Metro web bundler.
// All operations are no-ops; the app relies on API data on web.

const store = new Map<string, string>();

export function initDb() {}

export function getConfigValue(key: string): string | null {
  return store.get(key) ?? null;
}

export function setConfigValue(key: string, value: string) {
  store.set(key, value);
}

export function deleteConfigValue(key: string) {
  store.delete(key);
}

export function getLastSyncTime(): Date | null {
  const v = store.get("lastSyncAt");
  return v ? new Date(v) : null;
}

export function setLastSyncTime(d: Date) {
  store.set("lastSyncAt", d.toISOString());
}

export function getLocalHabits(_userId: string): Habit[] { return []; }
export function saveHabitsLocally(_habits: Habit[]) {}
export function deleteLocalHabit(_id: string) {}

export function getLocalCompletions(_userId: string, _from: string, _to: string): HabitCompletion[] { return []; }
export function saveCompletionsLocally(_completions: HabitCompletion[]) {}
export function deleteLocalCompletion(_habitId: string, _date: string) {}

export function deleteLocalUserData(_userId: string) {}
