/**
 * roomShift.ts — roster, check-in/check-out, and PIN confirmation calls (feature 008a).
 * All device-authenticated (apiClient attaches the device token automatically once paired —
 * see apiClient.ts's auth middleware).
 */
import { apiClient } from "./apiClient";
import { handleDeviceRejection } from "./deviceAuth";
import type { RoomRosterCard } from "../types";
import type { PinKeypadResult } from "../components/PinKeypad";

async function bodyOf(response: Response): Promise<{ errorKey?: string; lockedUntil?: string; attemptsRemaining?: number }> {
  return response.json().catch(() => ({}));
}

/** FR-021/022: a revoked or expired device token clears local credentials and returns to
 * room-setup — every device-authenticated call in this file checks for this uniformly. */
async function handleIfDeviceRejected(response: Response): Promise<boolean> {
  if (response.status !== 401) return false;
  const body = await bodyOf(response);
  if (body.errorKey === "errors.devices.revoked" || body.errorKey === "errors.devices.token_expired") {
    await handleDeviceRejection();
    return true;
  }
  return false;
}

export async function getRoster(): Promise<RoomRosterCard[]> {
  const result = await apiClient.GET("/api/room-shifts/roster");
  if (await handleIfDeviceRejected(result.response)) return [];
  if (!result.response.ok) throw new Error("roster_load_failed");
  return (await result.response.json()) as RoomRosterCard[];
}

/**
 * Check-in/out requires server-side PIN verification (FR-019 — no client-side validation is
 * possible), so unlike other offline-capable writes in this app, a network failure here can't
 * be optimistically queued as "checked in" — the app has no way to know locally whether the
 * PIN was even correct. A network error surfaces honestly as errors.network rather than being
 * queued or misreported as an invalid PIN; full offline check-in/out (a queued, unconfirmed
 * state with retroactive correction once synced) is not implemented in this pass.
 */
export async function checkIn(staffId: string, pin: string): Promise<PinKeypadResult> {
  let result;
  try {
    result = await apiClient.POST("/api/room-shifts/check-in", { body: { staffId, pin } });
  } catch {
    return { ok: false, errorKey: "errors.network" };
  }
  if (await handleIfDeviceRejected(result.response)) return { ok: false, errorKey: "errors.devices.revoked" };
  if (result.response.ok) return { ok: true };
  const body = await bodyOf(result.response);
  return { ok: false, errorKey: body.errorKey, lockedUntil: body.lockedUntil };
}

export async function checkOut(staffId: string, pin: string): Promise<PinKeypadResult> {
  let result;
  try {
    result = await apiClient.POST("/api/room-shifts/check-out", { body: { staffId, pin } });
  } catch {
    return { ok: false, errorKey: "errors.network" };
  }
  if (await handleIfDeviceRejected(result.response)) return { ok: false, errorKey: "errors.devices.revoked" };
  if (result.response.ok) return { ok: true };
  const body = await bodyOf(result.response);
  return { ok: false, errorKey: body.errorKey, lockedUntil: body.lockedUntil };
}

/** FR-017/018: sensitive-action administrator confirmation. `skip: true` always succeeds
 * (US5 AC2). Offline handling (US5 AC3 — skip straight to null, no API call) is the caller's
 * responsibility (AdministratorConfirmation.tsx checks useNetworkStatus() first). */
export async function confirmAdministrator(
  staffId: string | null, pin: string | null, skip: boolean,
): Promise<{ administeredByStaffProfileId: string | null } & PinKeypadResult> {
  const result = await apiClient.POST("/api/room-shifts/confirm-administrator", { body: { staffId, pin, skip } });
  if (await handleIfDeviceRejected(result.response))
    return { ok: false, errorKey: "errors.devices.revoked", administeredByStaffProfileId: null };
  if (result.response.ok) {
    const data = (await result.response.json()) as { administeredByStaffProfileId: string | null };
    return { ok: true, administeredByStaffProfileId: data.administeredByStaffProfileId };
  }
  const body = await bodyOf(result.response);
  return { ok: false, errorKey: body.errorKey, lockedUntil: body.lockedUntil, administeredByStaffProfileId: null };
}
