// Web shim for expo-secure-store — uses localStorage as the backing store.
// Keys are namespaced to avoid collisions with other storage entries.

const PREFIX = '__sss__';

export async function getItemAsync(key) {
  try { return localStorage.getItem(PREFIX + key) ?? null; } catch { return null; }
}

export async function setItemAsync(key, value) {
  try { localStorage.setItem(PREFIX + key, value); } catch {}
}

export async function deleteItemAsync(key) {
  try { localStorage.removeItem(PREFIX + key); } catch {}
}

export const AFTER_FIRST_UNLOCK = 0;
export const AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY = 1;
export const ALWAYS = 2;
export const ALWAYS_THIS_DEVICE_ONLY = 3;
export const WHEN_PASSCODE_SET_THIS_DEVICE_ONLY = 4;
export const WHEN_UNLOCKED = 5;
export const WHEN_UNLOCKED_THIS_DEVICE_ONLY = 6;
