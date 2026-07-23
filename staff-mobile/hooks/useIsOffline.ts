import { useEffect, useState } from "react";
import * as Network from "expo-network";

/**
 * A point-in-time connectivity check via expo-network, re-run whenever the caller's
 * `recheckKey` changes (e.g. each time a photo detail view opens) — this app has no other
 * offline-aware UI yet, so a live subscription would be premature (031-photo-lifecycle-
 * governance spec.md: "the download action is not available" while offline, since a live
 * signed URL is required).
 */
export function useIsOffline(recheckKey: unknown = null) {
  const [isOffline, setIsOffline] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Network.getNetworkStateAsync().then((state) => {
      if (!cancelled) setIsOffline(state.isConnected === false || state.isInternetReachable === false);
    });
    return () => {
      cancelled = true;
    };
  }, [recheckKey]);

  return isOffline;
}
