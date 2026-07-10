import { create } from "zustand";
import type { AuthState } from "../types";

interface AppState {
  // ── Auth ──────────────────────────────────────────────────────────────────
  auth: AuthState | null;

  // ── Auth actions ──────────────────────────────────────────────────────────
  setAuth:           (auth: AuthState) => void;
  updateAccessToken: (token: string) => void;
  resetAuth:         () => void;
}

export const useStore = create<AppState>((set, get) => ({
  auth: null,

  setAuth: (auth) => set({ auth }),
  updateAccessToken: (accessToken) => {
    const { auth } = get();
    if (auth) set({ auth: { ...auth, accessToken } });
  },
  resetAuth: () => set({ auth: null }),
}));
