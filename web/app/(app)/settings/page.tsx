"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { Input } from "../../../components/ui/input";
import { ErrorState } from "../../../components/ErrorState";
import type { OrganisationResponse, PaymentConnectionResponse, PaymentAuthorizationUrlResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
type ConnectionLoadState = "loading" | "loaded" | "error";
type ConnectionActionState = "idle" | "connecting" | "error";

/**
 * Feature 014 — org-wide KBO number, the only field this feature needed at the time. Feature
 * 014a adds a Payments section (Mollie connection) to this same flat page — no
 * "Settings > Payments" sub-hierarchy exists anywhere in web/ (research.md R7).
 */
export default function OrganisationSettingsPage() {
  const t = useTranslations("organisationSettings");
  const tp = useTranslations("organisationSettings.paymentConnection");
  const [organisation, setOrganisation] = useState<OrganisationResponse | null>(null);
  const [kboNumber, setKboNumber] = useState("");
  const [sepaCreditorId, setSepaCreditorId] = useState("");
  const [state, setState] = useState<LoadState>("loading");
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  const [connection, setConnection] = useState<PaymentConnectionResponse | null>(null);
  const [connectionState, setConnectionState] = useState<ConnectionLoadState>("loading");
  const [connectionAction, setConnectionAction] = useState<ConnectionActionState>("idle");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/organisations/me");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    const data = result.data as unknown as OrganisationResponse;
    setOrganisation(data);
    setKboNumber(data.kboNumber ?? "");
    setSepaCreditorId(data.sepaCreditorIdentifier ?? "");
    setState("loaded");
  }, []);

  const loadConnection = useCallback(async () => {
    setConnectionState("loading");
    const result = await apiClient.GET("/api/organisations/me/payment-connection");
    if (!result.response.ok) {
      setConnectionState("error");
      return;
    }
    setConnection(result.data as unknown as PaymentConnectionResponse);
    setConnectionState("loaded");
  }, []);

  useEffect(() => {
    load();
    loadConnection();
  }, [load, loadConnection]);

  async function save() {
    setSaving(true);
    setNotice("");
    const result = await apiClient.PUT("/api/organisations/me", {
      body: { kboNumber: kboNumber || null, sepaCreditorIdentifier: sepaCreditorId || null },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("saveError"));
      return;
    }
    setOrganisation(result.data as unknown as OrganisationResponse);
    setNotice(t("saveSuccess"));
  }

  async function connectMollie() {
    setConnectionAction("connecting");
    const result = await apiClient.POST("/api/organisations/me/payment-connection/authorize");
    if (!result.response.ok) {
      setConnectionAction("error");
      return;
    }
    const { authorizationUrl } = result.data as unknown as PaymentAuthorizationUrlResponse;
    window.location.href = authorizationUrl;
  }

  async function disconnectMollie() {
    setConnectionAction("connecting");
    const result = await apiClient.DELETE("/api/organisations/me/payment-connection");
    setConnectionAction("idle");
    if (!result.response.ok) {
      setConnectionAction("error");
      return;
    }
    await loadConnection();
  }

  if (state === "loading") return <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !organisation) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
      <div className="max-w-xl space-y-8">
        <div className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="kboNumber" className="text-sm font-medium text-text dark:text-text-dark">
              {t("kboNumberLabel")}
            </label>
            <Input id="kboNumber" value={kboNumber} onChange={(e) => setKboNumber(e.target.value)} />
          </div>
          <div className="space-y-2">
            <label htmlFor="sepaCreditorId" className="text-sm font-medium text-text dark:text-text-dark">
              {t("sepaCreditorIdLabel")}
            </label>
            <Input id="sepaCreditorId" value={sepaCreditorId} onChange={(e) => setSepaCreditorId(e.target.value)} />
            <p className="text-xs text-text-soft dark:text-text-soft-dark">{t("sepaCreditorIdHint")}</p>
          </div>
          {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}
          <Button onClick={save} disabled={saving}>
            {t("saveButton")}
          </Button>
        </div>

        <div className="space-y-3 border-t border-border pt-8 dark:border-border-dark">
          <h2 className="text-lg font-semibold text-text dark:text-text-dark">{tp("title")}</h2>

          {connectionState === "loading" && (
            <div className="h-16 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
          )}

          {connectionState === "error" && (
            <ErrorState message={tp("loadError")} retryLabel={tp("retry")} onRetry={loadConnection} />
          )}

          {connectionState === "loaded" && connection?.status === "connected" && (
            <div className="space-y-3 rounded-xl bg-surface-soft p-4 dark:bg-surface-soft-dark">
              <div className="flex items-center gap-2">
                <span className="inline-flex items-center gap-1 rounded-full bg-success/15 px-3 py-1 text-xs font-medium text-success dark:text-success-dark">
                  {tp("connected")}
                </span>
                {connection.providerAccountLabel && (
                  <span className="text-sm text-text-soft dark:text-text-soft-dark">{connection.providerAccountLabel}</span>
                )}
              </div>
              <Button variant="destructive" size="sm" onClick={disconnectMollie} disabled={connectionAction === "connecting"}>
                {tp("disconnect")}
              </Button>
            </div>
          )}

          {connectionState === "loaded" && connection?.status === "disconnected" && (
            <div className="space-y-3">
              <p className="text-sm text-text-soft dark:text-text-soft-dark">{tp("notConnectedDescription")}</p>
              {connectionAction === "error" && (
                <p className="text-sm text-danger dark:text-danger-dark">{tp("oauthFailed")}</p>
              )}
              <Button onClick={connectMollie} disabled={connectionAction === "connecting"}>
                {connectionAction === "connecting" ? tp("connecting") : tp("connect")}
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
