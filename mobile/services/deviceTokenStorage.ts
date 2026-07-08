/**
 * deviceTokenStorage.ts — the device-token SecureStore primitives, split out from
 * deviceAuth.ts specifically so apiClient.ts can import them without a circular
 * dependency: apiClient's auth middleware needs to read/write the token, and deviceAuth.ts
 * needs apiClient to make the pairing/exit-room-mode API calls (mirrors why auth.ts registers
 * a callback with apiClient instead of apiClient importing auth.ts directly).
 */
import * as SecureStore from "expo-secure-store";

const DEVICE_TOKEN_KEY = "childcare_device_token";

export const getDeviceToken = (): Promise<string | null> => SecureStore.getItemAsync(DEVICE_TOKEN_KEY);

export const storeDeviceToken = (token: string): Promise<void> => SecureStore.setItemAsync(DEVICE_TOKEN_KEY, token);

export const deleteStoredDeviceToken = (): Promise<void> => SecureStore.deleteItemAsync(DEVICE_TOKEN_KEY);
