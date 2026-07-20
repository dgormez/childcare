/**
 * roomShift.ts — roster, check-in/check-out, and PIN confirmation calls (feature 008a).
 * All device-authenticated (apiClient attaches the device token automatically once paired —
 * see apiClient.ts's auth middleware).
 *
 * Reads error/success bodies from openapi-fetch's own result.data/result.error, never from
 * result.response.json() — openapi-fetch already consumes the response body internally to
 * populate those, so a second read on result.response throws ("body stream already read"),
 * which masqueraded as a generic failure everywhere it was tried (found on-device: a real 200
 * success from /api/auth/login was being misreported as invalid credentials for this exact
 * reason — see auth.ts).
 */
import { apiClient } from "./apiClient";
import { handleDeviceRejection } from "./deviceAuth";
import type { RoomRosterResponse } from "../types";
import type { PinKeypadResult } from "../components/PinKeypad";

type ErrorBody = { errorKey?: string; lockedUntil?: string; attemptsRemaining?: number };

/** FR-021/022: a revoked or expired device token clears local credentials and returns to
 * room-setup — every device-authenticated call in this file checks for this uniformly. */
async function handleIfDeviceRejected(status: number, error: unknown): Promise<boolean> {
  if (status !== 401) return false;
  const errorKey = (error as ErrorBody | undefined)?.errorKey;
  if (errorKey === "errors.devices.revoked" || errorKey === "errors.devices.token_expired") {
    await handleDeviceRejection();
    return true;
  }
  return false;
}

const EMPTY_ROSTER: RoomRosterResponse = { requiresCaregiverPin: true, qrCheckInEnabled: false, caregivers: [] };

export async function getRoster(): Promise<RoomRosterResponse> {
  const result = await apiClient.GET("/api/room-shifts/roster");
  if (await handleIfDeviceRejected(result.response.status, result.error)) return EMPTY_ROSTER;
  if (!result.response.ok) throw new Error("roster_load_failed");
  return result.data as unknown as RoomRosterResponse;
}

/**
 * Check-in/out requires server-side PIN verification (FR-019 — no client-side validation is
 * possible), so unlike other offline-capable writes in this app, a network failure here can't
 * be optimistically queued as "checked in" — the app has no way to know locally whether the
 * PIN was even correct. A network error surfaces honestly as errors.network rather than being
 * queued or misreported as an invalid PIN; full offline check-in/out (a queued, unconfirmed
 * state with retroactive correction once synced) is not implemented in this pass.
 *
 * Feature 008b: `pin` is omitted entirely when the location's roster reported
 * `requiresCaregiverPin: false` — the server enforces this from the location's own setting
 * regardless of what (if anything) the client sends, per FR-007.
 */
// The generated CheckInRequest/CheckOutRequest schemas type `pin` as a required non-nullable
// `string` — ASP.NET Core's OpenAPI generation here doesn't reflect C#'s `string?` nullable
// reference type annotation, the same class of generated-type/reality gap this codebase already
// works around for response types (research.md R1). The cast below is the request-body version
// of that same workaround; the backend genuinely accepts `pin: null`.
type GeneratedCheckInOutBody = { staffId: string; pin: string };

export async function checkIn(staffId: string, pin?: string): Promise<PinKeypadResult> {
  let result;
  try {
    result = await apiClient.POST("/api/room-shifts/check-in", { body: { staffId, pin: pin ?? null } as unknown as GeneratedCheckInOutBody });
  } catch {
    return { ok: false, errorKey: "errors.network" };
  }
  if (await handleIfDeviceRejected(result.response.status, result.error)) return { ok: false, errorKey: "errors.devices.revoked" };
  if (result.response.ok) return { ok: true };
  const body = result.error as ErrorBody | undefined;
  return { ok: false, errorKey: body?.errorKey, lockedUntil: body?.lockedUntil };
}

export async function checkOut(staffId: string, pin?: string): Promise<PinKeypadResult> {
  let result;
  try {
    result = await apiClient.POST("/api/room-shifts/check-out", { body: { staffId, pin: pin ?? null } as unknown as GeneratedCheckInOutBody });
  } catch {
    return { ok: false, errorKey: "errors.network" };
  }
  if (await handleIfDeviceRejected(result.response.status, result.error)) return { ok: false, errorKey: "errors.devices.revoked" };
  if (result.response.ok) return { ok: true };
  const body = result.error as ErrorBody | undefined;
  return { ok: false, errorKey: body?.errorKey, lockedUntil: body?.lockedUntil };
}

/** FR-017/018: sensitive-action administrator confirmation. `skip: true` always succeeds
 * (US5 AC2). Offline handling (US5 AC3 — skip straight to null, no API call) is the caller's
 * responsibility (AdministratorConfirmation.tsx checks useNetworkStatus() first). */
export async function confirmAdministrator(
  staffId: string | null, pin: string | null, skip: boolean,
): Promise<{ administeredByStaffProfileId: string | null } & PinKeypadResult> {
  const result = await apiClient.POST("/api/room-shifts/confirm-administrator", { body: { staffId, pin, skip } });
  if (await handleIfDeviceRejected(result.response.status, result.error))
    return { ok: false, errorKey: "errors.devices.revoked", administeredByStaffProfileId: null };
  if (result.response.ok) {
    const data = result.data as unknown as { administeredByStaffProfileId: string | null };
    return { ok: true, administeredByStaffProfileId: data.administeredByStaffProfileId };
  }
  const body = result.error as ErrorBody | undefined;
  return { ok: false, errorKey: body?.errorKey, lockedUntil: body?.lockedUntil, administeredByStaffProfileId: null };
}
