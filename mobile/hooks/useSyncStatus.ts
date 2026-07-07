import { useCallback, useEffect, useState } from "react";
import { getPending } from "../services/offlineQueue";
import { getLastSyncTime } from "../services/localDb";
import { subscribeSyncState, isSyncingNow } from "../services/syncEngine";

export function useSyncStatus() {
  const [pendingCount, setPendingCount] = useState(0);
  const [lastSyncedAt, setLastSyncedAt] = useState<string | null>(null);
  const [isSyncing, setIsSyncing] = useState(isSyncingNow());

  const refresh = useCallback(async () => {
    try {
      const pending = await getPending();
      setPendingCount(pending.length);
    } catch {
      setPendingCount(0); // no active session (e.g. mid-logout) — nothing pending to report
    }
    const last = getLastSyncTime();
    setLastSyncedAt(last ? last.toISOString() : null);
    setIsSyncing(isSyncingNow());
  }, []);

  useEffect(() => {
    refresh();
    return subscribeSyncState(refresh);
  }, [refresh]);

  return { pendingCount, lastSyncedAt, isSyncing };
}
