import { create } from "zustand";
import type { AuthState, Habit, HabitCompletion } from "../types";
import type { SubscriptionStatus } from "../services/api";

interface AppState {
  // ── Auth ──────────────────────────────────────────────────────────────────
  auth: AuthState | null;

  // ── Data ──────────────────────────────────────────────────────────────────
  habits:      Habit[];
  completions: HabitCompletion[];

  // ── Subscription ──────────────────────────────────────────────────────────
  subscription: SubscriptionStatus | null;

  // ── UI ────────────────────────────────────────────────────────────────────
  isSyncing:  boolean;
  lastSyncAt: Date | null;
  isOnline:   boolean;

  // ── Auth actions ──────────────────────────────────────────────────────────
  setAuth:           (auth: AuthState) => void;
  updateAccessToken: (token: string) => void;
  resetAuth:         () => void;

  // ── Habits actions ────────────────────────────────────────────────────────
  setHabits:    (habits: Habit[]) => void;
  addHabit:     (habit: Habit) => void;
  updateHabit:  (habit: Habit) => void;
  removeHabit:  (id: string) => void;

  // ── Completions actions ───────────────────────────────────────────────────
  setCompletions:    (completions: HabitCompletion[]) => void;
  addCompletion:     (c: HabitCompletion) => void;
  removeCompletion:  (habitId: string, date: string) => void;

  // ── Subscription actions ───────────────────────────────────────────────────
  setSubscription: (sub: SubscriptionStatus | null) => void;

  // ── UI actions ────────────────────────────────────────────────────────────
  setSyncing:    (v: boolean) => void;
  setLastSyncAt: (date: Date) => void;
  setOnline:     (v: boolean) => void;
}

export const useStore = create<AppState>((set, get) => ({
  auth:         null,
  habits:       [],
  completions:  [],
  subscription: null,
  isSyncing:    false,
  lastSyncAt:   null,
  isOnline:     true,

  // Auth
  setAuth: (auth) => set({ auth }),
  updateAccessToken: (accessToken) => {
    const { auth } = get();
    if (auth) set({ auth: { ...auth, accessToken } });
  },
  resetAuth: () => set({ auth: null, habits: [], completions: [], subscription: null, lastSyncAt: null }),

  // Habits
  setHabits:   (habits)  => set({ habits }),
  addHabit:    (habit)   => set((s) => ({ habits: [...s.habits, habit] })),
  updateHabit: (habit)   => set((s) => ({ habits: s.habits.map((h) => h.id === habit.id ? habit : h) })),
  removeHabit: (id)      => set((s) => ({ habits: s.habits.filter((h) => h.id !== id) })),

  // Completions
  setCompletions:   (completions) => set({ completions }),
  addCompletion:    (c)           => set((s) => ({ completions: [...s.completions, c] })),
  removeCompletion: (habitId, date) =>
    set((s) => ({ completions: s.completions.filter((c) => !(c.habitId === habitId && c.date === date)) })),

  // Subscription
  setSubscription: (subscription) => set({ subscription }),

  // UI
  setSyncing:    (isSyncing)  => set({ isSyncing }),
  setLastSyncAt: (lastSyncAt) => set({ lastSyncAt }),
  setOnline:     (isOnline)   => set({ isOnline }),
}));
