"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Copy } from "lucide-react";
import { Button } from "./ui/button";
import { apiClient } from "../lib/apiClient";
import { useAuth } from "./AuthProvider";
import type { LocationResponse } from "../lib/types";

interface PublicEnrollmentSettingsFormProps {
  location: LocationResponse;
  onSaved: (updated: LocationResponse) => void;
}

/**
 * Feature 023 — FR-001/FR-002/FR-012/FR-013: mirrors QrCheckInSettingsForm's (021) edit-then-
 * explicit-save pattern exactly, including reverting the toggle to its last-saved value on a
 * failed save. Additionally surfaces the location's shareable public enrollment URL (built
 * client-side from the current origin — this codebase has no server-exposed
 * "public web base URL" config, only App:PublicEnrollmentBaseUrl on the API side for email
 * links) with a one-click copy action.
 */
export function PublicEnrollmentSettingsForm({ location, onSaved }: PublicEnrollmentSettingsFormProps) {
  const t = useTranslations("locations.publicEnrollmentSettings");
  const { session } = useAuth();
  const [enabled, setEnabled] = useState(location.publicEnrollmentEnabled);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");
  const [copied, setCopied] = useState(false);

  const publicUrl = typeof window !== "undefined" && session
    ? `${window.location.origin}/enroll/${session.organisationSlug}/${location.publicEnrollmentSlug}`
    : "";

  async function save() {
    setSaving(true);
    setNotice("");

    const result = await apiClient.PUT("/api/locations/{id}/public-enrollment-setting", {
      params: { path: { id: location.id } },
      body: { enabled },
    });
    setSaving(false);

    if (!result.response.ok) {
      setEnabled(location.publicEnrollmentEnabled);
      setNotice(t("saveError"));
      return;
    }

    setNotice(t("saveSuccess"));
    onSaved(result.data as unknown as LocationResponse);
  }

  async function copyUrl() {
    if (!publicUrl) return;
    await navigator.clipboard.writeText(publicUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="max-w-xl space-y-6">
      <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("description")}</p>

      <label className="flex items-center gap-2 text-sm font-medium text-text dark:text-text-dark">
        <input
          type="checkbox"
          checked={enabled}
          onChange={(e) => setEnabled(e.target.checked)}
          className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
        />
        {t("toggleLabel")}
      </label>

      <div className="space-y-2 rounded-lg bg-surface-soft p-4 text-sm text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark">
        <p>{t("explanation")}</p>
      </div>

      {publicUrl && (
        <div className="space-y-2">
          <p className="text-sm font-medium text-text dark:text-text-dark">{t("urlLabel")}</p>
          <div className="flex items-center gap-2">
            <code className="flex-1 truncate rounded-lg bg-surface-soft px-3 py-2 text-xs text-text dark:bg-surface-soft-dark dark:text-text-dark">
              {publicUrl}
            </code>
            <Button variant="secondary" size="sm" onClick={copyUrl}>
              <Copy className="mr-1 h-4 w-4" strokeWidth={2} />
              {copied ? t("copied") : t("copyAction")}
            </Button>
          </div>
        </div>
      )}

      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      <Button onClick={save} disabled={saving}>
        {t("saveButton")}
      </Button>
    </div>
  );
}
