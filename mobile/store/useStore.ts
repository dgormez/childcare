import { create } from "zustand";
import type { AuthState, DeviceState } from "../types";

interface AppState {
  // ── Auth ──────────────────────────────────────────────────────────────────
  auth: AuthState | null;

  // ── Auth actions ──────────────────────────────────────────────────────────
  setAuth:           (auth: AuthState) => void;
  updateAccessToken: (token: string) => void;
  resetAuth:         () => void;

  // ── Kiosk mode device (feature 008a) ─────────────────────────────────────
  device: DeviceState | null;

  // ── Device actions ────────────────────────────────────────────────────────
  setDevice:   (device: DeviceState) => void;
  resetDevice: () => void;
}

export const useStore = create<AppState>((set, get) => ({
  auth: null,

  setAuth: (auth) => set({ auth }),
  updateAccessToken: (accessToken) => {
    const { auth } = get();
    if (auth) set({ auth: { ...auth, accessToken } });
  },
  resetAuth: () => set({ auth: null }),

  device: null,

  setDevice: (device) => set({ device }),
  resetDevice: () => set({ device: null }),
}));
