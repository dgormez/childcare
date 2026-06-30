import { useCallback } from "react";
import dayjs from "dayjs";
import { useStore } from "../store/useStore";
import { checkHealth, fetchHabits, fetchCompletions } from "../services/api";
import { saveHabitsLocally, saveCompletionsLocally, setLastSyncTime } from "../services/localDb";

export function useSync() {
  const { setHabits, setCompletions, setSyncing, setOnline, setLastSyncAt } = useStore();

  const sync = useCallback(async () => {
    setSyncing(true);
    try {
      const online = await checkHealth();
      setOnline(online);
      if (!online) return;

      const today   = dayjs().format("YYYY-MM-DD");
      const weekAgo = dayjs().subtract(6, "day").format("YYYY-MM-DD");

      const [habits, completions] = await Promise.all([
        fetchHabits(),
        fetchCompletions(weekAgo, today),
      ]);

      saveHabitsLocally(habits);
      saveCompletionsLocally(completions);
      setHabits(habits);
      setCompletions(completions);

      const syncDate = new Date();
      setLastSyncTime(syncDate);
      setLastSyncAt(syncDate);
    } catch (err: unknown) {
      if ((err as Error).message === "SESSION_EXPIRED") {
        useStore.getState().resetAuth();
      } else {
        console.warn("[sync] failed:", err);
      }
    } finally {
      setSyncing(false);
    }
  }, [setHabits, setCompletions, setSyncing, setOnline, setLastSyncAt]);

  return { sync };
}
