"use client";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { getSubscriptionStatus } from "../../lib/api";

export default function PaymentSuccessPage() {
  const router = useRouter();

  useEffect(() => {
    let cancelled = false;
    async function pollUntilActive() {
      for (let i = 0; i < 8; i++) {
        await new Promise(r => setTimeout(r, 1500));
        if (cancelled) return;
        try {
          const data = await getSubscriptionStatus();
          if (data.isActive) break;
        } catch { /* keep polling */ }
      }
      if (!cancelled) router.replace("/subscription");
    }
    pollUntilActive();
    return () => { cancelled = true; };
  }, [router]);

  return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="text-center">
        <p className="text-6xl mb-4">🎉</p>
        <h1 className="text-2xl font-bold text-gray-900 mb-2">You&apos;re all set!</h1>
        <p className="text-gray-500 mb-6">Welcome to Pro. Redirecting you now…</p>
        <div className="w-6 h-6 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto" />
      </div>
    </div>
  );
}
