"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "./ui/button";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse } from "../lib/types";

interface QrCheckInSettingsFormProps {
  location: LocationResponse;
  onSaved: (updated: LocationResponse) => void;
}

/**
 * Feature 021 — FR-001/FR-002/FR-003/FR-018: mirrors CheckInSettingsForm's (008b) edit-then-
 * explicit-save pattern exactly, including reverting the toggle to its last-saved value on a
 * failed save rather than leaving the UI showing an unsaved state as if it succeeded.
 */
export function QrCheckInSettingsForm({ location, onSaved }: QrCheckInSettingsFormProps) {
  const t = useTranslations("locations.qrCheckInSettings");
  const [enabled, setEnabled] = useState(location.qrCheckInEnabled);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  async function save() {
    setSaving(true);
    setNotice("");

    const result = await apiClient.PUT("/api/locations/{id}/qr-checkin-setting", {
      params: { path: { id: location.id } },
      body: { enabled },
    });
    setSaving(false);

    if (!result.response.ok) {
      setEnabled(location.qrCheckInEnabled);
      setNotice(t("saveError"));
      return;
    }

    setNotice(t("saveSuccess"));
    onSaved(result.data as unknown as LocationResponse);
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
        <p>{t("explanationAdds")}</p>
        <p>{t("explanationUnchanged")}</p>
      </div>

      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      <Button onClick={save} disabled={saving}>
        {t("saveButton")}
      </Button>
    </div>
  );
}
