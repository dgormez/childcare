/**
 * localDb.ts — SQLite persistence layer (expo-sqlite).
 *
 * Schema:
 *   config            — key/value store for auth state, last-sync timestamp
 *   habits            — local copy of the user's habits (synced from API)
 *   habit_completions — daily completions (synced from API)
 */
import { Platform } from "react-native";
import * as SQLite from "expo-sqlite";
import type { SQLiteDatabase } from "expo-sqlite";
import type { Habit, HabitCompletion } from "../types";

const db: SQLiteDatabase = Platform.OS !== "web"
  ? SQLite.openDatabaseSync("twinstack.db")
  : (null as unknown as SQLiteDatabase);

// ── Schema ────────────────────────────────────────────────────────────────────

export function initDb() {
  if (Platform.OS === "web") return;
  db.execSync(`
    PRAGMA journal_mode = WAL;

    CREATE TABLE IF NOT EXISTS config (
      key   TEXT PRIMARY KEY,
      value TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS habits (
      id        TEXT PRIMARY KEY,
      userId    TEXT NOT NULL,
      name      TEXT NOT NULL,
      color     TEXT NOT NULL DEFAULT '#3b82f6',
      icon      TEXT NOT NULL DEFAULT '✅',
      createdAt TEXT NOT NULL
    );

    CREATE INDEX IF NOT EXISTS idx_habits_user ON habits(userId, createdAt DESC);

    CREATE TABLE IF NOT EXISTS habit_completions (
      id        TEXT PRIMARY KEY,
      habitId   TEXT NOT NULL,
      userId    TEXT NOT NULL,
      date      TEXT NOT NULL,
      createdAt TEXT NOT NULL,
      UNIQUE(habitId, date)
    );

    CREATE INDEX IF NOT EXISTS idx_completions_user_date ON habit_completions(userId, date);
  `);
}

// ── Config ────────────────────────────────────────────────────────────────────

export function getConfigValue(key: string): string | null {
  if (Platform.OS === "web") return localStorage.getItem(key);
  return db.getFirstSync<{ value: string }>(
    "SELECT value FROM config WHERE key = ?", [key]
  )?.value ?? null;
}

export function setConfigValue(key: string, value: string) {
  if (Platform.OS === "web") { localStorage.setItem(key, value); return; }
  db.runSync("INSERT OR REPLACE INTO config (key, value) VALUES (?, ?)", [key, value]);
}

export function deleteConfigValue(key: string) {
  if (Platform.OS === "web") { localStorage.removeItem(key); return; }
  db.runSync("DELETE FROM config WHERE key = ?", [key]);
}

// ── Sync timestamp ────────────────────────────────────────────────────────────

export function getLastSyncTime(): Date | null {
  const v = getConfigValue("lastSyncAt");
  return v ? new Date(v) : null;
}

export function setLastSyncTime(d: Date) {
  setConfigValue("lastSyncAt", d.toISOString());
}

// ── Habits ────────────────────────────────────────────────────────────────────

export function getLocalHabits(userId: string): Habit[] {
  if (Platform.OS === "web") return [];
  return db.getAllSync<Habit>(
    "SELECT * FROM habits WHERE userId = ? ORDER BY createdAt ASC",
    [userId]
  );
}

export function saveHabitsLocally(habits: Habit[]) {
  if (Platform.OS === "web") return;
  const stmt = db.prepareSync(
    "INSERT OR REPLACE INTO habits (id, userId, name, color, icon, createdAt) VALUES (?,?,?,?,?,?)"
  );
  try {
    for (const h of habits)
      stmt.executeSync([h.id, h.userId, h.name, h.color, h.icon, h.createdAt]);
  } finally {
    stmt.finalizeSync();
  }
}

export function deleteLocalHabit(id: string) {
  if (Platform.OS === "web") return;
  db.runSync("DELETE FROM habits WHERE id = ?", [id]);
  db.runSync("DELETE FROM habit_completions WHERE habitId = ?", [id]);
}

// ── Completions ───────────────────────────────────────────────────────────────

export function getLocalCompletions(userId: string, from: string, to: string): HabitCompletion[] {
  if (Platform.OS === "web") return [];
  return db.getAllSync<HabitCompletion>(
    "SELECT * FROM habit_completions WHERE userId = ? AND date >= ? AND date <= ?",
    [userId, from, to]
  );
}

export function saveCompletionsLocally(completions: HabitCompletion[]) {
  if (Platform.OS === "web") return;
  const stmt = db.prepareSync(
    "INSERT OR REPLACE INTO habit_completions (id, habitId, userId, date, createdAt) VALUES (?,?,?,?,?)"
  );
  try {
    for (const c of completions)
      stmt.executeSync([c.id, c.habitId, c.userId, c.date, c.createdAt]);
  } finally {
    stmt.finalizeSync();
  }
}

export function deleteLocalCompletion(habitId: string, date: string) {
  if (Platform.OS === "web") return;
  db.runSync("DELETE FROM habit_completions WHERE habitId = ? AND date = ?", [habitId, date]);
}

/** Wipes all user data — called on logout. */
export function deleteLocalUserData(userId: string) {
  if (Platform.OS === "web") return;
  db.runSync("DELETE FROM habits WHERE userId = ?", [userId]);
  db.runSync("DELETE FROM habit_completions WHERE userId = ?", [userId]);
}
