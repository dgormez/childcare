"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { Input } from "../../../components/ui/input";
import { ErrorState } from "../../../components/ErrorState";
import type { OrganisationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Feature 014 — org-wide KBO number, the only field this feature needs. No org-settings screen
 * existed in web/ before this; deliberately a small single-field page, not a full settings
 * dashboard (nothing else in this codebase needs one yet).
 */
export default function OrganisationSettingsPage() {
  const t = useTranslations("organisationSettings");
  const [organisation, setOrganisation] = useState<OrganisationResponse | null>(null);
  const [kboNumber, setKboNumber] = useState("");
  const [state, setState] = useState<LoadState>("loading");
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

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
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function save() {
    setSaving(true);
    setNotice("");
    const result = await apiClient.PUT("/api/organisations/me", {
      body: { kboNumber: kboNumber || null },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("saveError"));
      return;
    }
    setOrganisation(result.data as unknown as OrganisationResponse);
    setNotice(t("saveSuccess"));
  }

  if (state === "loading") return <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !organisation) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
      <div className="max-w-xl space-y-4">
        <div className="space-y-2">
          <label htmlFor="kboNumber" className="text-sm font-medium text-text dark:text-text-dark">
            {t("kboNumberLabel")}
          </label>
          <Input id="kboNumber" value={kboNumber} onChange={(e) => setKboNumber(e.target.value)} />
        </div>
        {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}
        <Button onClick={save} disabled={saving}>
          {t("saveButton")}
        </Button>
      </div>
    </div>
  );
}
