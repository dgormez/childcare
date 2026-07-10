/**
 * pushToken.ts — User Story 5 (T102): requests notification permission, obtains the device's
 * Expo push token, and registers it with the backend (PUT /api/parent/push-token), which
 * overwrites any previously-registered token for this parent account (FR-014 — one active
 * token per user, last write wins, so a reinstall doesn't leave a stale token active).
 *
 * Called from auth.ts on every successful login/refresh — best-effort and non-blocking: a
 * parent who declines the permission or is running in an environment with no push capability
 * (e.g. a simulator) still has the in-app notification centre (User Story 4) as a fallback.
 */
import * as Notifications from "expo-notifications";
import Constants from "expo-constants";
import { apiClient } from "./apiClient";

export async function registerPushToken(): Promise<void> {
  const existing = await Notifications.getPermissionsAsync();
  let status = existing.status;

  if (status !== "granted") {
    const requested = await Notifications.requestPermissionsAsync();
    status = requested.status;
  }
  if (status !== "granted") return;

  // Required by SDK 53+ (Constants.expoConfig?.extra?.eas?.projectId — same "YOUR_EAS_PROJECT_ID"
  // placeholder convention as mobile/app.config.js; getExpoPushTokenAsync throws without one).
  const projectId = Constants.expoConfig?.extra?.eas?.projectId as string | undefined;
  if (!projectId || projectId === "YOUR_EAS_PROJECT_ID") return;

  const { data: pushToken } = await Notifications.getExpoPushTokenAsync({ projectId });
  if (!pushToken) return;

  await apiClient.PUT("/api/parent/push-token", {
    body: { pushToken },
  });
}
