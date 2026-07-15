"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowLeft } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { Button } from "../../../../components/ui/button";
import { Input } from "../../../../components/ui/input";
import { ErrorState } from "../../../../components/ErrorState";
import { ReservationSettingsForm } from "../../../../components/ReservationSettingsForm";
import { CheckInSettingsForm } from "../../../../components/CheckInSettingsForm";
import { MenuVariantSettingsForm } from "../../../../components/MenuVariantSettingsForm";
import { InvoiceSettingsForm } from "../../../../components/InvoiceSettingsForm";
import type { LocationResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
type Tab = "general" | "reservationSettings" | "checkInSettings" | "menuVariants" | "invoiceSettings";

/**
 * "Algemeen" tab is deliberately minimal — the core editable fields only. The Opgroeien
 * reporting fields (naamLocatie/dossiernummer/verantwoordelijke/flexPermission/boPermission,
 * feature 004) round-trip through PUT /api/locations/{id} unchanged; a full settings UI for
 * them is out of this feature's scope (research.md R5 — this feature exists to host the
 * Reserveringsinstellingen tab, not to build full location administration).
 */
function GeneralLocationForm({ location, onSaved }: { location: LocationResponse; onSaved: (l: LocationResponse) => void }) {
  const t = useTranslations("locations.general");
  const [name, setName] = useState(location.name);
  const [address, setAddress] = useState(location.address);
  const [phone, setPhone] = useState(location.phone);
  const [email, setEmail] = useState(location.email);
  const [maxCapacity, setMaxCapacity] = useState(String(location.maxCapacity));
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  async function save() {
    setSaving(true);
    setNotice("");
    const result = await apiClient.PUT("/api/locations/{id}", {
      params: { path: { id: location.id } },
      body: {
        name,
        address,
        phone,
        email,
        maxCapacity: Number(maxCapacity) || 0,
        naamLocatie: location.naamLocatie,
        dossiernummer: location.dossiernummer,
        verantwoordelijke: location.verantwoordelijke,
        flexPermission: location.flexPermission,
        boPermission: location.boPermission,
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
      <div className="space-y-2">
        <label htmlFor="name" className="text-sm font-medium text-text dark:text-text-dark">{t("nameLabel")}</label>
        <Input id="name" value={name} onChange={(e) => setName(e.target.value)} />
      </div>
      <div className="space-y-2">
        <label htmlFor="address" className="text-sm font-medium text-text dark:text-text-dark">{t("addressLabel")}</label>
        <Input id="address" value={address} onChange={(e) => setAddress(e.target.value)} />
      </div>
      <div className="space-y-2">
        <label htmlFor="phone" className="text-sm font-medium text-text dark:text-text-dark">{t("phoneLabel")}</label>
        <Input id="phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
      </div>
      <div className="space-y-2">
        <label htmlFor="email" className="text-sm font-medium text-text dark:text-text-dark">{t("emailLabel")}</label>
        <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      </div>
      <div className="space-y-2">
        <label htmlFor="maxCapacity" className="text-sm font-medium text-text dark:text-text-dark">{t("capacityLabel")}</label>
        <Input id="maxCapacity" type="number" min={1} value={maxCapacity} onChange={(e) => setMaxCapacity(e.target.value)} className="max-w-[8rem] tabular-nums" />
      </div>
      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}
      <Button onClick={save} disabled={saving}>{t("saveButton")}</Button>
    </div>
  );
}

export default function LocationDetailPage() {
  const t = useTranslations("locations");
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const [location, setLocation] = useState<LocationResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [tab, setTab] = useState<Tab>("general");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/locations/{id}", { params: { path: { id: params.id } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setLocation(result.data as unknown as LocationResponse);
    setState("loaded");
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  if (state === "loading") return <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error" || !location) return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  return (
    <div>
      <button
        onClick={() => router.push("/locations")}
        className="mb-4 flex items-center gap-2 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("backToList")}
      </button>

      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{location.name}</h1>

      <div className="mb-6 flex gap-1 border-b border-border dark:border-border-dark">
        <button
          onClick={() => setTab("general")}
          className={`px-4 py-2 text-sm font-medium ${
            tab === "general"
              ? "border-b-2 border-primary text-primary-hover dark:border-primary-dark dark:text-primary-hover-dark"
              : "text-text-soft dark:text-text-soft-dark"
          }`}
        >
          {t("tabGeneral")}
        </button>
        <button
          onClick={() => setTab("reservationSettings")}
          className={`px-4 py-2 text-sm font-medium ${
            tab === "reservationSettings"
              ? "border-b-2 border-primary text-primary-hover dark:border-primary-dark dark:text-primary-hover-dark"
              : "text-text-soft dark:text-text-soft-dark"
          }`}
        >
          {t("tabReservationSettings")}
        </button>
        <button
          onClick={() => setTab("checkInSettings")}
          className={`px-4 py-2 text-sm font-medium ${
            tab === "checkInSettings"
              ? "border-b-2 border-primary text-primary-hover dark:border-primary-dark dark:text-primary-hover-dark"
              : "text-text-soft dark:text-text-soft-dark"
          }`}
        >
          {t("tabCheckInSettings")}
        </button>
        <button
          onClick={() => setTab("menuVariants")}
          className={`px-4 py-2 text-sm font-medium ${
            tab === "menuVariants"
              ? "border-b-2 border-primary text-primary-hover dark:border-primary-dark dark:text-primary-hover-dark"
              : "text-text-soft dark:text-text-soft-dark"
          }`}
        >
          {t("tabMenuVariants")}
        </button>
        <button
          onClick={() => setTab("invoiceSettings")}
          className={`px-4 py-2 text-sm font-medium ${
            tab === "invoiceSettings"
              ? "border-b-2 border-primary text-primary-hover dark:border-primary-dark dark:text-primary-hover-dark"
              : "text-text-soft dark:text-text-soft-dark"
          }`}
        >
          {t("tabInvoiceSettings")}
        </button>
      </div>

      {tab === "general" && <GeneralLocationForm location={location} onSaved={setLocation} />}
      {tab === "reservationSettings" && <ReservationSettingsForm location={location} onSaved={setLocation} />}
      {tab === "checkInSettings" && <CheckInSettingsForm location={location} onSaved={setLocation} />}
      {tab === "menuVariants" && <MenuVariantSettingsForm location={location} onSaved={setLocation} />}
      {tab === "invoiceSettings" && <InvoiceSettingsForm location={location} onSaved={setLocation} />}
    </div>
  );
}
