"use client";
import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { NextIntlClientProvider, useTranslations } from "next-intl";
import { FileSignature } from "lucide-react";
import { publicApiClient } from "../../lib/publicApiClient";
import { Input } from "../../components/ui/input";
import { Button } from "../../components/ui/button";
import { SignatureCapture, type SignatureValue } from "../../components/SignatureCapture";
import type { ContractForSigningResponse } from "../../lib/types";
import enMessages from "../../i18n/locales/en.json";
import nlMessages from "../../i18n/locales/nl.json";
import frMessages from "../../i18n/locales/fr.json";

type SupportedLocale = "nl" | "fr" | "en";
const MESSAGES: Record<SupportedLocale, typeof enMessages> = { en: enMessages, nl: nlMessages, fr: frMessages };
const LOCALE_LABELS: Record<SupportedLocale, string> = { nl: "Nederlands", fr: "Français", en: "English" };

type PageState = "loading" | "invalidLink" | "form" | "submitting" | "submitted";

const CONSENT_FIELDS = ["photosInternal", "photosWebsite", "photosSocialMedia", "videoInternal", "photosPress"] as const;

/**
 * Feature 024-esignature (User Story 1). Public, unauthenticated route outside `(app)`/`(auth)`
 * (research.md R1, mirrors feature 023's `/enroll/[orgSlug]/[locationSlug]`) — reached only via
 * the emailed signing link (`/sign?org=...&token=...`), never linked from anywhere in the app
 * itself. FR-012: every failure mode (expired/used/tampered/unknown token) collapses to the same
 * calm "invalidLink" state — this page never tries to distinguish which one occurred.
 */
export default function PublicContractSigningPage() {
  const searchParams = useSearchParams();
  const org = searchParams.get("org") ?? "";
  const token = searchParams.get("token") ?? "";

  const [locale, setLocale] = useState<SupportedLocale>("nl");
  const [localeInitialized, setLocaleInitialized] = useState(false);
  const [pageState, setPageState] = useState<PageState>("loading");
  const [contract, setContract] = useState<ContractForSigningResponse | null>(null);
  const [signature, setSignature] = useState<SignatureValue | null>(null);
  const [confirmSignIntent, setConfirmSignIntent] = useState(false);
  const [iban, setIban] = useState("");
  const [confirmSepaMandate, setConfirmSepaMandate] = useState(false);
  const [ibanError, setIbanError] = useState("");
  const [submitError, setSubmitError] = useState("");

  useEffect(() => {
    if (!org || !token) {
      setPageState("invalidLink");
      return;
    }
    publicApiClient
      .GET("/api/public/contracts/sign", { params: { query: { org, token } } })
      .then((result) => {
        if (!result.response.ok) {
          setPageState("invalidLink");
          return;
        }
        const data = result.data as unknown as ContractForSigningResponse;
        setContract(data);
        if (!localeInitialized) {
          const defaultLocale = data.locale as SupportedLocale;
          if (defaultLocale in MESSAGES) setLocale(defaultLocale);
          setLocaleInitialized(true);
        }
        setPageState("form");
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [org, token]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!signature || !confirmSignIntent || !confirmSepaMandate) return;

    setPageState("submitting");
    setIbanError("");
    setSubmitError("");

    const result = await publicApiClient.POST("/api/public/contracts/sign", {
      params: { query: { org, token } },
      body: { signatureType: signature.signatureType, signatureData: signature.signatureData, sepaIban: iban.trim() },
    });

    if (result.response.status === 404) {
      setPageState("invalidLink");
      return;
    }
    if (result.response.status === 422) {
      const body = (result.error ?? {}) as { errorKey?: string };
      setIbanError(body.errorKey === "errors.contract_signing.invalid_iban" ? "invalid" : "");
      setPageState("form");
      return;
    }
    if (!result.response.ok) {
      setSubmitError("generic");
      setPageState("form");
      return;
    }

    setPageState("submitted");
  }

  return (
    <NextIntlClientProvider locale={locale} messages={MESSAGES[locale]}>
      <PageBody
        pageState={pageState}
        contract={contract}
        locale={locale}
        onLocaleChange={setLocale}
        onSignatureChange={setSignature}
        confirmSignIntent={confirmSignIntent}
        onConfirmSignIntentChange={setConfirmSignIntent}
        iban={iban}
        onIbanChange={setIban}
        ibanError={ibanError}
        confirmSepaMandate={confirmSepaMandate}
        onConfirmSepaMandateChange={setConfirmSepaMandate}
        submitError={submitError}
        canSubmit={Boolean(signature && confirmSignIntent && confirmSepaMandate && iban.trim())}
        onSubmit={submit}
      />
    </NextIntlClientProvider>
  );
}

interface PageBodyProps {
  pageState: PageState;
  contract: ContractForSigningResponse | null;
  locale: SupportedLocale;
  onLocaleChange: (locale: SupportedLocale) => void;
  onSignatureChange: (value: SignatureValue | null) => void;
  confirmSignIntent: boolean;
  onConfirmSignIntentChange: (value: boolean) => void;
  iban: string;
  onIbanChange: (value: string) => void;
  ibanError: string;
  confirmSepaMandate: boolean;
  onConfirmSepaMandateChange: (value: boolean) => void;
  submitError: string;
  canSubmit: boolean;
  onSubmit: (e: React.FormEvent) => void;
}

function PageBody({
  pageState, contract, locale, onLocaleChange, onSignatureChange, confirmSignIntent, onConfirmSignIntentChange,
  iban, onIbanChange, ibanError, confirmSepaMandate, onConfirmSepaMandateChange, submitError, canSubmit, onSubmit,
}: PageBodyProps) {
  const t = useTranslations("contractSigning");

  return (
    <main className="mx-auto min-h-screen max-w-lg px-4 py-8">
      <div className="mb-8 flex items-center gap-3">
        <FileSignature className="h-6 w-6 text-primary dark:text-primary-dark" strokeWidth={2} />
        <h1 className="text-xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
      </div>

      {pageState === "loading" && <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}

      {pageState === "invalidLink" && (
        <div className="space-y-4">
          {/* FR-019: the language toggle stays available even on a dead link — a parent whose
              expired link renders in the wrong default locale still needs to read this message. */}
          <LanguageToggle t={t} locale={locale} onLocaleChange={onLocaleChange} />
          <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("invalidLink")}</p>
        </div>
      )}

      {pageState === "submitted" && (
        <div className="space-y-2">
          <p className="text-base font-medium text-text dark:text-text-dark">{t("confirmationTitle")}</p>
          <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("confirmationBody")}</p>
        </div>
      )}

      {(pageState === "form" || pageState === "submitting") && contract && (
        <form onSubmit={onSubmit} className="space-y-6" noValidate aria-live="polite">
          <LanguageToggle t={t} locale={locale} onLocaleChange={onLocaleChange} />

          <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("intro")}</p>

          {/* FR-005: the full contract terms render before any signature/IBAN input below. */}
          <div className="space-y-2 rounded-xl bg-surface-soft p-4 text-sm dark:bg-surface-soft-dark">
            <p>
              <span className="font-medium text-text dark:text-text-dark">{t("childLabel")}: </span>
              {contract.childName}
            </p>
            <p>
              <span className="font-medium text-text dark:text-text-dark">{t("locationLabel")}: </span>
              {contract.locationName}
            </p>
            <div>
              <span className="font-medium text-text dark:text-text-dark">{t("contractedDaysLabel")}: </span>
              <ul className="ml-4 list-disc">
                {contract.contractedDays.map((day, i) => (
                  <li key={i}>
                    {day.weekday} {day.startTime}–{day.endTime}
                  </li>
                ))}
              </ul>
            </div>
            <p className="tabular-nums">
              <span className="font-medium text-text dark:text-text-dark">{t("dailyRateLabel")}: </span>
              {(contract.dailyRateCents / 100).toFixed(2)}
            </p>
            <div>
              <span className="font-medium text-text dark:text-text-dark">{t("consentLabel")}: </span>
              <ul className="ml-4 list-disc">
                {CONSENT_FIELDS.map((field) => (
                  <li key={field}>
                    {t(`consent.${field}`)}: {contract.consent[field] ? t("yes") : t("no")}
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <div>
            <h2 className="mb-2 text-base font-semibold text-text dark:text-text-dark">{t("signatureTitle")}</h2>
            <SignatureCapture onChange={onSignatureChange} />
          </div>

          <label className="flex items-center gap-2 text-sm text-text dark:text-text-dark">
            <input
              type="checkbox"
              checked={confirmSignIntent}
              onChange={(e) => onConfirmSignIntentChange(e.target.checked)}
              className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
            />
            {t("confirmSignIntent")}
          </label>

          <div className="space-y-3 border-t border-border pt-6 dark:border-border-dark">
            <h2 className="text-base font-semibold text-text dark:text-text-dark">{t("sepaTitle")}</h2>
            <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("sepaIntro")}</p>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("ibanLabel")}
              <Input
                className="mt-2"
                invalid={Boolean(ibanError)}
                value={iban}
                onChange={(e) => onIbanChange(e.target.value)}
                autoComplete="off"
              />
              {ibanError && (
                <p role="alert" className="mt-1 text-xs text-danger dark:text-danger-dark">
                  {t("ibanInvalid")}
                </p>
              )}
            </label>
            <label className="flex items-center gap-2 text-sm text-text dark:text-text-dark">
              <input
                type="checkbox"
                checked={confirmSepaMandate}
                onChange={(e) => onConfirmSepaMandateChange(e.target.checked)}
                className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
              />
              {t("confirmSepaMandate")}
            </label>
          </div>

          {submitError && (
            <p role="alert" className="text-sm text-danger dark:text-danger-dark">
              {t("submitError")}
            </p>
          )}

          <Button type="submit" disabled={!canSubmit || pageState === "submitting"} className="w-full">
            {pageState === "submitting" && (
              <span
                aria-hidden="true"
                className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent"
              />
            )}
            {pageState === "submitting" ? t("submittingAction") : t("submitAction")}
          </Button>
        </form>
      )}
    </main>
  );
}

interface LanguageToggleProps {
  t: ReturnType<typeof useTranslations>;
  locale: SupportedLocale;
  onLocaleChange: (locale: SupportedLocale) => void;
}

function LanguageToggle({ t, locale, onLocaleChange }: LanguageToggleProps) {
  return (
    <div className="flex gap-2" role="group" aria-label={t("languageLabel")}>
      {(Object.keys(LOCALE_LABELS) as SupportedLocale[]).map((code) => (
        <button
          key={code}
          type="button"
          onClick={() => onLocaleChange(code)}
          aria-pressed={locale === code}
          className={`h-10 rounded-lg px-3 text-sm font-medium ${
            locale === code
              ? "bg-primary text-white"
              : "bg-surface-soft text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark"
          }`}
        >
          {LOCALE_LABELS[code]}
        </button>
      ))}
    </div>
  );
}
