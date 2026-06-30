"use client";
import { useState, useEffect } from "react";
import { getSubscriptionStatus, createCheckoutSession, createPortalSession } from "../../../lib/api";
import type { SubscriptionStatus } from "../../../lib/types";

export default function SubscriptionPage() {
  const [sub,     setSub]     = useState<SubscriptionStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [error,   setError]   = useState("");

  useEffect(() => {
    getSubscriptionStatus().then(setSub).finally(() => setLoading(false));
  }, []);

  const renewsOn = sub?.currentPeriodEnd
    ? new Date(sub.currentPeriodEnd).toLocaleDateString("en-US", { month: "long", day: "numeric", year: "numeric" })
    : null;

  const handleSubscribe = async () => {
    setWorking(true); setError("");
    try {
      const origin     = window.location.origin;
      const successUrl = `${origin}/payment-success`;
      const cancelUrl  = `${origin}/subscription`;
      const { url }    = await createCheckoutSession(successUrl, cancelUrl);
      window.location.href = url;
    } catch {
      setError("Could not start checkout. Please try again.");
    } finally {
      setWorking(false);
    }
  };

  const handleManage = async () => {
    setWorking(true); setError("");
    try {
      const returnUrl = `${window.location.origin}/subscription`;
      const { url }   = await createPortalSession(returnUrl);
      window.location.href = url;
    } catch {
      setError("Could not open portal. Please try again.");
    } finally {
      setWorking(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  const isActive = sub?.isActive ?? false;

  return (
    <div className="max-w-md">
      <h1 className="text-2xl font-bold text-gray-900 mb-1">Pro Plan</h1>
      <p className="text-gray-500 mb-6">Unlock unlimited habits and premium features.</p>

      {/* Status card */}
      <div className={`bg-white border rounded-xl p-5 mb-6 ${isActive ? "border-green-400" : "border-gray-200"}`}>
        <div className="flex items-center gap-2 mb-2">
          <div className={`w-2.5 h-2.5 rounded-full ${isActive ? "bg-green-500" : "bg-gray-300"}`} />
          <span className="font-semibold text-gray-900">
            {isActive ? "Active" : (sub?.status || "No subscription")}
          </span>
        </div>
        {renewsOn && (
          <p className="text-sm text-gray-500">
            {sub?.status === "Canceled" ? "Access until" : "Renews"} {renewsOn}
          </p>
        )}
        {sub?.status === "Trialing" && (
          <p className="text-sm text-blue-600 mt-1 font-medium">
            ⏳ You&apos;re in your free trial — no charge until it ends
          </p>
        )}
      </div>

      {error && <p className="text-red-500 text-sm mb-4">{error}</p>}

      {isActive ? (
        <div className="space-y-3">
          <button
            onClick={handleManage} disabled={working}
            className="w-full bg-white border border-gray-200 hover:border-gray-300 text-gray-900 font-semibold py-3 rounded-xl text-sm transition disabled:opacity-50"
          >
            {working ? "Opening portal…" : "Manage Subscription"}
          </button>
          <p className="text-xs text-gray-400 text-center">Cancel, update payment, or view invoices in the Stripe portal</p>
        </div>
      ) : (
        <div className="space-y-4">
          {/* Feature list */}
          <ul className="space-y-2 mb-2">
            {["Unlimited habits", "Full history & streaks", "Priority support", "Future premium features"].map((f) => (
              <li key={f} className="flex items-center gap-2 text-sm text-gray-700">
                <span className="text-green-500 font-bold">✓</span> {f}
              </li>
            ))}
          </ul>
          <button
            onClick={handleSubscribe} disabled={working}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold py-3.5 rounded-xl transition disabled:opacity-50"
          >
            {working ? "Redirecting…" : "Start 14-day free trial"}
          </button>
          <p className="text-xs text-gray-400 text-center">No credit card required to start • Cancel anytime</p>
        </div>
      )}
    </div>
  );
}
