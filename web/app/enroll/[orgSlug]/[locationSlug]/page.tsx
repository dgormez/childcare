"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { NextIntlClientProvider, useTranslations } from "next-intl";
import { Heart } from "lucide-react";
import { publicApiClient } from "../../../../lib/publicApiClient";
import { Input } from "../../../../components/ui/input";
import { Button } from "../../../../components/ui/button";
import type { GetPublicEnrollmentLocationInfoResponse, SubmitPublicEnrollmentResponse } from "../../../../lib/types";
import enMessages from "../../../../i18n/locales/en.json";
import nlMessages from "../../../../i18n/locales/nl.json";
import frMessages from "../../../../i18n/locales/fr.json";

type SupportedLocale = "nl" | "fr" | "en";
const MESSAGES: Record<SupportedLocale, typeof enMessages> = { en: enMessages, nl: nlMessages, fr: frMessages };
const LOCALE_LABELS: Record<SupportedLocale, string> = { nl: "Nederlands", fr: "Français", en: "English" };

type PageState = "loading" | "notFound" | "disabled" | "form" | "submitted" | "rateLimited";

interface FormValues {
  childFirstName: string;
  childLastName: string;
  dateOfBirth: string;
  requestedStartDate: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  notes: string;
  website: string; // honeypot — never rendered as a visible field for a real user
}

const EMPTY_FORM: FormValues = {
  childFirstName: "",
  childLastName: "",
  dateOfBirth: "",
  requestedStartDate: "",
  contactName: "",
  contactEmail: "",
  contactPhone: "",
  notes: "",
  website: "",
};

/**
 * Feature 023 — the public, unauthenticated enrollment form (spec.md User Story 1). Lives
 * outside the `(app)`/`(auth)` route groups (research.md R1) so it's reachable with no session
 * at all; the root layout's AuthProvider still wraps it harmlessly (this page never calls
 * useAuth()). Its own NextIntlClientProvider (nested, with locale-specific messages loaded
 * client-side) lets the language toggle switch this page's copy independent of the app-wide
 * locale cookie — no [locale] URL segment or app-wide switcher exists yet (i18n/request.ts).
 */
export default function PublicEnrollmentPage() {
  const params = useParams<{ orgSlug: string; locationSlug: string }>();
  const [locale, setLocale] = useState<SupportedLocale>("nl");
  const [localeInitialized, setLocaleInitialized] = useState(false);
  const [pageState, setPageState] = useState<PageState>("loading");
  const [locationName, setLocationName] = useState("");
  const [values, setValues] = useState<FormValues>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [referenceCode, setReferenceCode] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    publicApiClient
      .GET("/api/public/enrollment/{orgSlug}/{locationSlug}", {
        params: { path: { orgSlug: params.orgSlug, locationSlug: params.locationSlug } },
      })
      .then((result) => {
        if (!result.response.ok) {
          setPageState("notFound");
          return;
        }
        const data = result.data as unknown as GetPublicEnrollmentLocationInfoResponse;
        setLocationName(data.locationName);
        if (!localeInitialized) {
          const defaultLocale = data.defaultLocale as SupportedLocale;
          if (defaultLocale in MESSAGES) setLocale(defaultLocale);
          setLocaleInitialized(true);
        }
        setPageState(data.enabled ? "form" : "disabled");
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.orgSlug, params.locationSlug]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setFieldErrors({});

    const result = await publicApiClient.POST("/api/public/enrollment/{orgSlug}/{locationSlug}", {
      params: { path: { orgSlug: params.orgSlug, locationSlug: params.locationSlug } },
      body: {
        childFirstName: values.childFirstName.trim(),
        childLastName: values.childLastName.trim(),
        dateOfBirth: values.dateOfBirth,
        requestedStartDate: values.requestedStartDate || null,
        contactName: values.contactName.trim(),
        contactEmail: values.contactEmail.trim(),
        contactPhone: values.contactPhone.trim() || null,
        notes: values.notes.trim() || null,
        locale,
        website: values.website,
      },
    });
    setSubmitting(false);

    if (result.response.status === 429) {
      setPageState("rateLimited");
      return;
    }
    if (result.response.status === 403) {
      setPageState("disabled");
      return;
    }
    if (!result.response.ok) {
      const body = (result.error ?? {}) as { fieldErrors?: Record<string, string> };
      setFieldErrors(body.fieldErrors ?? {});
      return;
    }

    setReferenceCode((result.data as unknown as SubmitPublicEnrollmentResponse).referenceCode);
    setPageState("submitted");
  }

  return (
    <NextIntlClientProvider locale={locale} messages={MESSAGES[locale]}>
      <PageBody
        pageState={pageState}
        locationName={locationName}
        locale={locale}
        onLocaleChange={setLocale}
        values={values}
        onValuesChange={setValues}
        fieldErrors={fieldErrors}
        onSubmit={submit}
        submitting={submitting}
        referenceCode={referenceCode}
      />
    </NextIntlClientProvider>
  );
}

interface PageBodyProps {
  pageState: PageState;
  locationName: string;
  locale: SupportedLocale;
  onLocaleChange: (locale: SupportedLocale) => void;
  values: FormValues;
  onValuesChange: (updater: (v: FormValues) => FormValues) => void;
  fieldErrors: Record<string, string>;
  onSubmit: (e: React.FormEvent) => void;
  submitting: boolean;
  referenceCode: string;
}

function PageBody({
  pageState, locationName, locale, onLocaleChange, values, onValuesChange, fieldErrors, onSubmit, submitting, referenceCode,
}: PageBodyProps) {
  const t = useTranslations("publicEnrollment");

  function field(key: keyof FormValues) {
    return {
      value: values[key],
      onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
        onValuesChange((v) => ({ ...v, [key]: e.target.value })),
    };
  }

  return (
    <main className="mx-auto min-h-screen max-w-lg px-4 py-16">
      <div className="mb-8 flex items-center gap-3">
        <Heart className="h-6 w-6 text-primary dark:text-primary-dark" strokeWidth={2} />
        <h1 className="text-xl font-semibold text-text dark:text-text-dark">
          {locationName || t("title")}
        </h1>
      </div>

      {pageState === "loading" && <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}

      {pageState === "notFound" && (
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("notFound")}</p>
      )}

      {pageState === "disabled" && (
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("disabled")}</p>
      )}

      {pageState === "rateLimited" && (
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("rateLimited")}</p>
      )}

      {pageState === "submitted" && (
        <div className="space-y-4">
          <p className="text-sm text-text dark:text-text-dark">{t("confirmationBody")}</p>
          <p className="rounded-lg bg-surface-soft p-4 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
            {t("referenceLabel")}: <strong className="font-mono">{referenceCode}</strong>
          </p>
        </div>
      )}

      {pageState === "form" && (
        <form onSubmit={onSubmit} className="space-y-6" noValidate aria-live="polite">
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

          <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("intro")}</p>

          <div className="grid grid-cols-2 gap-4">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("childFirstNameLabel")}
              <Input className="mt-2" invalid={!!fieldErrors.ChildFirstName} required {...field("childFirstName")} />
              {fieldErrors.ChildFirstName && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.ChildFirstName}</p>}
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("childLastNameLabel")}
              <Input className="mt-2" invalid={!!fieldErrors.ChildLastName} required {...field("childLastName")} />
              {fieldErrors.ChildLastName && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.ChildLastName}</p>}
            </label>
            <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
              {t("dateOfBirthLabel")}
              <Input className="mt-2" type="date" invalid={!!fieldErrors.DateOfBirth} required {...field("dateOfBirth")} />
              {fieldErrors.DateOfBirth && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.DateOfBirth}</p>}
            </label>
            <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
              {t("requestedStartDateLabel")}
              <Input className="mt-2" type="date" {...field("requestedStartDate")} />
            </label>
            <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
              {t("contactNameLabel")}
              <Input className="mt-2" invalid={!!fieldErrors.ContactName} required {...field("contactName")} />
              {fieldErrors.ContactName && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.ContactName}</p>}
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("contactEmailLabel")}
              <Input className="mt-2" type="email" invalid={!!fieldErrors.ContactEmail} required {...field("contactEmail")} />
              {fieldErrors.ContactEmail && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.ContactEmail}</p>}
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("contactPhoneLabel")}
              <Input className="mt-2" {...field("contactPhone")} />
            </label>
            <label className="col-span-2 block text-sm font-medium text-text dark:text-text-dark">
              {t("notesLabel")}
              <Input className="mt-2" {...field("notes")} />
            </label>
          </div>

          {/* Honeypot (FR-005) — visually hidden, never shown to a real user, no autofill hint */}
          <div aria-hidden="true" className="absolute -left-[9999px] top-auto h-px w-px overflow-hidden">
            <label>
              Website
              <input type="text" tabIndex={-1} autoComplete="off" {...field("website")} />
            </label>
          </div>

          <Button type="submit" disabled={submitting} className="w-full">
            {t("submitAction")}
          </Button>
        </form>
      )}
    </main>
  );
}
