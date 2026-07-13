"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "./ui/button";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse } from "../lib/types";

interface CheckInSettingsFormProps {
  location: LocationResponse;
  onSaved: (updated: LocationResponse) => void;
}

/**
 * Feature 008b — FR-003/FR-015: the tradeoff copy is shown unconditionally alongside the
 * toggle (not a secondary disclosure). Edit-then-explicit-save mirrors GeneralLocationForm/
 * ReservationSettingsForm's pattern on the sibling tabs of this same page (design-system.md:
 * reuse the established pattern rather than introduce a third, auto-saving one). A failed save
 * reverts the toggle to its last-saved value rather than leaving the UI showing an unsaved
 * state as if it succeeded.
 */
export function CheckInSettingsForm({ location, onSaved }: CheckInSettingsFormProps) {
  const t = useTranslations("locations.checkInSettings");
  const [requiresCaregiverPin, setRequiresCaregiverPin] = useState(location.requiresCaregiverPin);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  async function save() {
    setSaving(true);
    setNotice("");

    const result = await apiClient.PUT("/api/locations/{id}/checkin-settings", {
      params: { path: { id: location.id } },
      body: { requiresCaregiverPin },
    });
    setSaving(false);

    if (!result.response.ok) {
      setRequiresCaregiverPin(location.requiresCaregiverPin);
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
          checked={requiresCaregiverPin}
          onChange={(e) => setRequiresCaregiverPin(e.target.checked)}
          className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
        />
        {t("toggleLabel")}
      </label>

      <div className="space-y-2 rounded-lg bg-surface-soft p-4 text-sm text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark">
        <p>{t("tradeoffIdentity")}</p>
        <p>{t("tradeoffRisk")}</p>
      </div>

      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      <Button onClick={save} disabled={saving}>
        {t("saveButton")}
      </Button>
    </div>
  );
}
