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
  const [erkenningsnummer, setErkenningsnummer] = useState(location.erkenningsnummer ?? "");
  const [bankAccountNumber, setBankAccountNumber] = useState(location.bankAccountNumber ?? "");
  const [invoiceDueDays, setInvoiceDueDays] = useState(String(location.invoiceDueDays));
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

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

  return (
    <div className="max-w-xl space-y-4">
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
  );
}
