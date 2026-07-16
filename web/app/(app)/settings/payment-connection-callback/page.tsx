"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { apiClient } from "../../../../lib/apiClient";

type CallbackState = "processing" | "success" | "error";

/**
 * Feature 014a — Mollie's OAuth redirect target (contracts/014a-invoice-payments-plus/
 * payments-api.md). The (app) layout above this page already blocks rendering until the
 * director's session is restored from the httpOnly refresh cookie (AuthProvider), so this
 * effect never races an unauthenticated apiClient call after the full-page navigation to and
 * from Mollie's domain.
 */
export default function PaymentConnectionCallbackPage() {
  const t = useTranslations("organisationSettings.paymentConnection");
  const router = useRouter();
  const searchParams = useSearchParams();
  const [state, setState] = useState<CallbackState>("processing");
  const ranOnce = useRef(false);

  useEffect(() => {
    if (ranOnce.current) return;
    ranOnce.current = true;

    const code = searchParams.get("code");
    if (!code) {
      setState("error");
      return;
    }

    apiClient
      .POST("/api/organisations/me/payment-connection/callback", { body: { authorizationCode: code } })
      .then((result) => setState(result.response.ok ? "success" : "error"));
  }, [searchParams]);

  useEffect(() => {
    if (state === "success") {
      const timeout = setTimeout(() => router.replace("/settings"), 1500);
      return () => clearTimeout(timeout);
    }
  }, [state, router]);

  return (
    <div className="flex flex-col items-center justify-center gap-4 py-16 text-center">
      {state === "processing" && (
        <>
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
          <p className="text-text-soft dark:text-text-soft-dark">{t("connecting")}</p>
        </>
      )}
      {state === "success" && <p className="text-success dark:text-success-dark">{t("connected")}</p>}
      {state === "error" && (
        <>
          <p className="text-danger dark:text-danger-dark">{t("oauthFailed")}</p>
          <button
            className="text-sm text-primary-hover underline dark:text-primary-hover-dark"
            onClick={() => router.replace("/settings")}
          >
            {t("backToSettings")}
          </button>
        </>
      )}
    </div>
  );
}
