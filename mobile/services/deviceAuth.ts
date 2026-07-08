/**
 * deviceAuth.ts — pairing, device-token storage/rotation, and revocation handling for kiosk
 * mode (feature 008a).
 *
 * Mirrors auth.ts's pattern: SecureStore for the credential itself (never AsyncStorage — FR-002),
 * the plain config table for display-only cached state, and the Zustand device slice as one
 * consistent operation. The token primitives themselves live in deviceTokenStorage.ts so
 * apiClient.ts can read/write them without importing this file (avoids a circular import,
 * mirrors why auth.ts registers a callback with apiClient instead of the reverse).
 */
import { apiClient } from "./apiClient";
import { getDeviceToken, storeDeviceToken, deleteStoredDeviceToken } from "./deviceTokenStorage";
import { getConfigValue, setConfigValue, deleteConfigValue, deleteLocalTenantData } from "./localDb";
import { useStore } from "../store/useStore";
import type { DeviceState, PairDeviceResponse } from "../types";

export { getDeviceToken, storeDeviceToken };

/** Persists pairing state (device id, location/group) after a successful pair — the device
 * token itself is stored separately via storeDeviceToken. */
export function applyDevicePairing(device: DeviceState) {
  setConfigValue("deviceId", device.deviceId);
  setConfigValue("deviceLocationId", device.locationId);
  setConfigValue("deviceGroupId", device.groupId);
  setConfigValue("deviceLocationName", device.locationName);
  setConfigValue("deviceGroupName", device.groupName);
  useStore.getState().setDevice(device);
}

/** FR-022: clears everything this tablet knows about its pairing — device token, cached
 * location/group display state, and the in-memory device slice. Used by both explicit
 * director-override exit and the revoked/expired rejection path (see handleDeviceRejection). */
export async function clearDeviceCredentials(): Promise<void> {
  await deleteStoredDeviceToken();
  deleteConfigValue("deviceId");
  deleteConfigValue("deviceLocationId");
  deleteConfigValue("deviceGroupId");
  deleteConfigValue("deviceLocationName");
  deleteConfigValue("deviceGroupName");
  useStore.getState().resetDevice();
}

/** Called on app startup. Restores device-mode display state from cached config (offline-safe)
 * — the token itself is read separately by apiClient's auth middleware, whichever component
 * needs it. Returns true if this tablet is paired (device token present in SecureStore). */
export async function tryRestoreDeviceState(): Promise<boolean> {
  const token = await getDeviceToken();
  if (!token) return false;

  const deviceId = getConfigValue("deviceId");
  const locationId = getConfigValue("deviceLocationId");
  const groupId = getConfigValue("deviceGroupId");
  const locationName = getConfigValue("deviceLocationName");
  const groupName = getConfigValue("deviceGroupName");

  if (deviceId && locationId && groupId && locationName && groupName) {
    useStore.getState().setDevice({ deviceId, locationId, groupId, locationName, groupName });
    return true;
  }
  return false;
}

/** FR-021/FR-022: a revoked or expired device token clears all locally stored credentials and
 * cached tenant data, returning the tablet to the pairing/setup flow. */
export async function handleDeviceRejection(): Promise<void> {
  const tenantSlug = getConfigValue("organisationSlug");
  await clearDeviceCredentials();
  if (tenantSlug) deleteLocalTenantData(tenantSlug);
}

/**
 * FR-001/FR-002: one-time director pairing. The director is already signed in (their own user
 * JWT is attached by apiClient at this point, since no device token exists yet). On success,
 * stores the device token and pairing state — from here on, apiClient prioritizes the device
 * token over any user session (research.md).
 */
export async function pairDevice(
  locationId: string,
  groupId: string,
  locationName: string,
  groupName: string,
  directorOverridePin: string,
): Promise<void> {
  const result = await apiClient.POST("/api/devices/pair", {
    body: { locationId, groupId, directorOverridePin },
  });
  if (!result.response.ok) {
    const body = await result.response.json().catch(() => ({}));
    throw new Error((body as { errorKey?: string }).errorKey ?? "errors.devices.pair_failed");
  }

  const data = (await result.response.json()) as PairDeviceResponse;
  await storeDeviceToken(data.deviceToken);
  applyDevicePairing({ deviceId: data.deviceId, locationId, groupId, locationName, groupName });
}

/** FR-005: director exits room mode via the device's own override PIN — device-authenticated,
 * not a user JWT. On success, clears all local pairing state and returns to setup. */
export async function exitRoomMode(directorOverridePin: string): Promise<void> {
  const result = await apiClient.POST("/api/devices/exit-room-mode", {
    body: { directorOverridePin },
  });
  if (!result.response.ok) {
    const body = await result.response.json().catch(() => ({}));
    throw new Error((body as { errorKey?: string }).errorKey ?? "errors.devices.invalid_override_pin");
  }

  await clearDeviceCredentials();
}
