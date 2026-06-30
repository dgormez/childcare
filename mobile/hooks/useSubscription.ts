import { useState, useCallback } from "react";
import * as WebBrowser from "expo-web-browser";
import { getSubscriptionStatus, createCheckoutSession, createPortalSession } from "../services/api";
import { useStore } from "../store/useStore";

// After checkout Stripe fires a webhook that updates the DB. Poll until the
// status reflects the payment rather than showing stale data immediately.
async function pollUntilActive(
  setSubscription: (s: Awaited<ReturnType<typeof getSubscriptionStatus>>) => void,
  attempts = 8,
  delayMs  = 1500,
) {
  for (let i = 0; i < attempts; i++) {
    await new Promise(r => setTimeout(r, delayMs));
    try {
      const data = await getSubscriptionStatus();
      setSubscription(data);
      if (data.isActive) return;
    } catch {
      // network blip — keep polling
    }
  }
}

export function useSubscription() {
  const { subscription, setSubscription } = useStore();
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const data = await getSubscriptionStatus();
      setSubscription(data);
    } catch (err) {
      console.warn("[subscription] refresh failed:", err);
    }
  }, [setSubscription]);

  const subscribe = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { url } = await createCheckoutSession();
      const result  = await WebBrowser.openAuthSessionAsync(url, "childcare://payment-success");
      if (result.type === "success") await pollUntilActive(setSubscription);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, [setSubscription]);

  const manage = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { url } = await createPortalSession();
      const result  = await WebBrowser.openAuthSessionAsync(url, "childcare://");
      if (result.type === "success") await refresh();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, [refresh]);

  return { subscription, loading, error, refresh, subscribe, manage };
}
