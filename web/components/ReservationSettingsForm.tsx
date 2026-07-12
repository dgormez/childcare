"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { ConfirmDialog } from "./ConfirmDialog";
import { apiClient } from "../lib/apiClient";
import type { ApiErrorBody, LocationResponse, ReservationRequestMode } from "../lib/types";

interface ReservationSettingsFormProps {
  location: LocationResponse;
  onSaved: (updated: LocationResponse) => void;
}

const MODES: ReservationRequestMode[] = ["disabled", "informational", "approval"];

/** Native <select> per the day-reservations queue's own status-filter precedent
 * (web/app/(app)/requests/page.tsx) — no shadcn Select primitive exists yet in this codebase,
 * and one native filter doesn't justify adding a new UI primitive (design-system.md: reuse
 * shared components rather than reimplement). */
function ModeSelect({
  id,
  value,
  onChange,
  t,
}: {
  id: string;
  value: ReservationRequestMode;
  onChange: (mode: ReservationRequestMode) => void;
  t: (key: string) => string;
}) {
  return (
    <select
      id={id}
      value={value}
      onChange={(e) => onChange(e.target.value as ReservationRequestMode)}
      className="h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
    >
      {MODES.map((mode) => (
        <option key={mode} value={mode}>
          {t(`mode${mode.charAt(0).toUpperCase()}${mode.slice(1)}`)}
        </option>
      ))}
    </select>
  );
}

export function ReservationSettingsForm({ location, onSaved }: ReservationSettingsFormProps) {
  const t = useTranslations("locations.reservationSettings");
  const [absencesMode, setAbsencesMode] = useState<ReservationRequestMode>(location.reservationAbsencesMode);
  const [extrasMode, setExtrasMode] = useState<ReservationRequestMode>(location.reservationExtrasMode);
  const [swapsMode, setSwapsMode] = useState<ReservationRequestMode>(location.reservationSwapsMode);
  const [noticeHours, setNoticeHours] = useState(String(location.reservationNoticeHours));
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");
  const [pendingWarning, setPendingWarning] = useState<Record<string, number> | null>(null);

  async function save(confirmDespitePending: boolean) {
    setSaving(true);
    setNotice("");
    const result = await apiClient.PUT("/api/locations/{id}/reservation-settings", {
      params: { path: { id: location.id } },
      body: {
        absencesMode,
        extrasMode,
        swapsMode,
        noticeHours: Number(noticeHours) || 0,
        confirmDespitePending,
      },
    });
    setSaving(false);

    if (!result.response.ok) {
      const error = (result.error ?? {}) as ApiErrorBody;
      if (error.errorKey === "errors.location.reservation_settings.pending_requests_warning" && error.pendingCounts) {
        setPendingWarning(error.pendingCounts as Record<string, number>);
        return;
      }
      setNotice(t("saveError"));
      return;
    }

    setPendingWarning(null);
    setNotice(t("saveSuccess"));
    onSaved(result.data as unknown as LocationResponse);
  }

  const warningBodyText = pendingWarning
    ? `${t("warningBody")} ${t("warningCounts", {
        absence: pendingWarning.absence ?? 0,
        extra: pendingWarning.extra ?? 0,
        exchange: pendingWarning.exchange ?? 0,
      })}`
    : "";

  return (
    <div className="max-w-xl space-y-6">
      <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("description")}</p>

      <div className="space-y-2">
        <label htmlFor="absencesMode" className="text-sm font-medium text-text dark:text-text-dark">
          {t("absencesLabel")}
        </label>
        <ModeSelect id="absencesMode" value={absencesMode} onChange={setAbsencesMode} t={t} />
      </div>

      <div className="space-y-2">
        <label htmlFor="extrasMode" className="text-sm font-medium text-text dark:text-text-dark">
          {t("extrasLabel")}
        </label>
        <ModeSelect id="extrasMode" value={extrasMode} onChange={setExtrasMode} t={t} />
      </div>

      <div className="space-y-2">
        <label htmlFor="swapsMode" className="text-sm font-medium text-text dark:text-text-dark">
          {t("swapsLabel")}
        </label>
        <ModeSelect id="swapsMode" value={swapsMode} onChange={setSwapsMode} t={t} />
      </div>

      <div className="space-y-2">
        <label htmlFor="noticeHours" className="text-sm font-medium text-text dark:text-text-dark">
          {t("noticeHoursLabel")}
        </label>
        <Input
          id="noticeHours"
          type="number"
          min={0}
          max={8760}
          value={noticeHours}
          onChange={(e) => setNoticeHours(e.target.value)}
          className="max-w-[8rem] tabular-nums"
        />
        <p className="text-xs text-text-soft dark:text-text-soft-dark">{t("noticeHoursHint")}</p>
      </div>

      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      <Button onClick={() => save(false)} disabled={saving}>
        {t("saveButton")}
      </Button>

      <ConfirmDialog
        open={pendingWarning !== null}
        onOpenChange={(open) => !open && setPendingWarning(null)}
        title={t("warningTitle")}
        description={warningBodyText}
        confirmLabel={t("warningConfirm")}
        cancelLabel={t("warningCancel")}
        confirming={saving}
        onConfirm={() => save(true)}
      />
    </div>
  );
}
