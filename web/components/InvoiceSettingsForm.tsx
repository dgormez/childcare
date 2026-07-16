"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse } from "../lib/types";

interface InvoiceSettingsFormProps {
  location: LocationResponse;
  onSaved: (updated: LocationResponse) => void;
}

/** Feature 014 FR-005/FR-005a. Mirrors ReservationSettingsForm.tsx's structure. */
export function InvoiceSettingsForm({ location, onSaved }: InvoiceSettingsFormProps) {
  const t = useTranslations("locations.invoiceSettings");
  const tr = useTranslations("locations.paymentReminderSettings");
  const [erkenningsnummer, setErkenningsnummer] = useState(location.erkenningsnummer ?? "");
  const [bankAccountNumber, setBankAccountNumber] = useState(location.bankAccountNumber ?? "");
  const [invoiceDueDays, setInvoiceDueDays] = useState(String(location.invoiceDueDays));
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  // Feature 014a — separate save action from the fields above, since it's a distinct backend
  // command/endpoint (UpdateLocationPaymentReminderSettingsCommand), mirroring how this same
  // "Invoicing" tab already keeps CheckInSettingsForm's toggle-plus-tradeoff-copy pattern on its
  // own PUT call rather than folding every setting into one giant form.
  const [remindersEnabled, setRemindersEnabled] = useState(location.paymentRemindersEnabled);
  const [delayDays, setDelayDays] = useState(String(location.paymentReminderDelayDays));
  const [cadenceDays, setCadenceDays] = useState(String(location.paymentReminderCadenceDays));
  const [savingReminders, setSavingReminders] = useState(false);
  const [reminderNotice, setReminderNotice] = useState("");

  async function save() {
    setSaving(true);
    setNotice("");
    const result = await apiClient.PUT("/api/locations/{id}/invoice-settings", {
      params: { path: { id: location.id } },
      body: {
        erkenningsnummer: erkenningsnummer || null,
        bankAccountNumber: bankAccountNumber || null,
        invoiceDueDays: Number(invoiceDueDays) || 0,
      },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("saveError"));
      return;
    }
    setNotice(t("saveSuccess"));
    onSaved(result.data as unknown as LocationResponse);
  }

  async function saveReminders() {
    setSavingReminders(true);
    setReminderNotice("");
    const result = await apiClient.PUT("/api/locations/{id}/payment-reminder-settings", {
      params: { path: { id: location.id } },
      body: {
        enabled: remindersEnabled,
        delayDays: Number(delayDays) || 0,
        cadenceDays: Number(cadenceDays) || 1,
      },
    });
    setSavingReminders(false);
    if (!result.response.ok) {
      setRemindersEnabled(location.paymentRemindersEnabled);
      setReminderNotice(tr("saveError"));
      return;
    }
    setReminderNotice(tr("saveSuccess"));
    onSaved(result.data as unknown as LocationResponse);
  }

  return (
    <div className="max-w-xl space-y-8">
      <div className="space-y-4">
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("description")}</p>
        <div className="space-y-2">
          <label htmlFor="erkenningsnummer" className="text-sm font-medium text-text dark:text-text-dark">
            {t("erkenningsnummerLabel")}
          </label>
          <Input id="erkenningsnummer" value={erkenningsnummer} onChange={(e) => setErkenningsnummer(e.target.value)} />
        </div>
        <div className="space-y-2">
          <label htmlFor="bankAccountNumber" className="text-sm font-medium text-text dark:text-text-dark">
            {t("bankAccountNumberLabel")}
          </label>
          <Input id="bankAccountNumber" value={bankAccountNumber} onChange={(e) => setBankAccountNumber(e.target.value)} />
        </div>
        <div className="space-y-2">
          <label htmlFor="invoiceDueDays" className="text-sm font-medium text-text dark:text-text-dark">
            {t("invoiceDueDaysLabel")}
          </label>
          <Input
            id="invoiceDueDays"
            type="number"
            min={0}
            value={invoiceDueDays}
            onChange={(e) => setInvoiceDueDays(e.target.value)}
            className="max-w-[8rem] tabular-nums"
          />
          <p className="text-xs text-text-soft dark:text-text-soft-dark">{t("invoiceDueDaysHint")}</p>
        </div>
        {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}
        <Button onClick={save} disabled={saving}>
          {t("saveButton")}
        </Button>
      </div>

      <div className="space-y-4 border-t border-border pt-8 dark:border-border-dark">
        <h3 className="text-sm font-semibold text-text dark:text-text-dark">{tr("title")}</h3>
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{tr("description")}</p>

        <label className="flex items-center gap-2 text-sm font-medium text-text dark:text-text-dark">
          <input
            type="checkbox"
            checked={remindersEnabled}
            onChange={(e) => setRemindersEnabled(e.target.checked)}
            className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
          />
          {tr("enabledLabel")}
        </label>

        <div className="flex gap-4">
          <div className="space-y-2">
            <label htmlFor="delayDays" className="text-sm font-medium text-text dark:text-text-dark">
              {tr("delayDaysLabel")}
            </label>
            <Input
              id="delayDays"
              type="number"
              min={0}
              value={delayDays}
              onChange={(e) => setDelayDays(e.target.value)}
              className="max-w-[8rem] tabular-nums"
            />
          </div>
          <div className="space-y-2">
            <label htmlFor="cadenceDays" className="text-sm font-medium text-text dark:text-text-dark">
              {tr("cadenceDaysLabel")}
            </label>
            <Input
              id="cadenceDays"
              type="number"
              min={1}
              value={cadenceDays}
              onChange={(e) => setCadenceDays(e.target.value)}
              className="max-w-[8rem] tabular-nums"
            />
          </div>
        </div>
        <p className="text-xs text-text-soft dark:text-text-soft-dark">{tr("capHint")}</p>

        {reminderNotice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{reminderNotice}</p>}
        <Button onClick={saveReminders} disabled={savingReminders}>
          {tr("saveButton")}
        </Button>
      </div>
    </div>
  );
}
