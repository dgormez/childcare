"use client";
import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { NextIntlClientProvider, useTranslations } from "next-intl";
import { Heart } from "lucide-react";
import { publicApiClient } from "../../lib/publicApiClient";
import { Input } from "../../components/ui/input";
import { Button } from "../../components/ui/button";
import type { InvitationInfoResponse } from "../../lib/types";
import enMessages from "../../i18n/locales/en.json";
import nlMessages from "../../i18n/locales/nl.json";
import frMessages from "../../i18n/locales/fr.json";

type SupportedLocale = "nl" | "fr" | "en";
const MESSAGES: Record<SupportedLocale, typeof enMessages> = { en: enMessages, nl: nlMessages, fr: frMessages };
const LOCALE_LABELS: Record<SupportedLocale, string> = { nl: "Nederlands", fr: "Français", en: "English" };

type PageState = "loading" | "invalid" | "form" | "submitted";

/**
 * Feature 032 — the public, unauthenticated registration page (research.md R7). Lives outside
 * the (app)/(auth) route groups so it's reachable with no session at all, mirroring
 * web/app/enroll/[orgSlug]/[locationSlug]/page.tsx's precedent: its own NextIntlClientProvider
 * with locale-specific messages loaded client-side, since a prospective director has no stored
 * locale preference yet. The email is pre-filled and locked (spec.md AC1) via a lookup against
 * GET /api/organisations/register/{token} — found necessary during implementation, since
 * feature 001's registration endpoint alone only validates the token at final submission.
 */
export default function RegisterPage() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [locale, setLocale] = useState<SupportedLocale>("nl");
  const [pageState, setPageState] = useState<PageState>("loading");
  const [email, setEmail] = useState("");
  const [organisationName, setOrganisationName] = useState("");
  const [directorName, setDirectorName] = useState("");
  const [password, setPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!token) {
      setPageState("invalid");
      return;
    }
    publicApiClient
      .GET("/api/organisations/register/{token}", { params: { path: { token } } })
      .then((result) => {
        if (!result.response.ok) {
          setPageState("invalid");
          return;
        }
        setEmail((result.data as unknown as InvitationInfoResponse).email);
        setPageState("form");
      });
  }, [token]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setFieldErrors({});

    const result = await publicApiClient.POST("/api/organisations/register", {
      body: {
        invitationToken: token,
        organisationName: organisationName.trim(),
        directorName: directorName.trim(),
        email,
        password,
      },
    });
    setSubmitting(false);

    if (result.response.status === 404) {
      setPageState("invalid");
      return;
    }
    if (!result.response.ok) {
      const body = (result.error ?? {}) as { fieldErrors?: Record<string, string> };
      setFieldErrors(body.fieldErrors ?? {});
      return;
    }

    setPageState("submitted");
  }

  return (
    <NextIntlClientProvider locale={locale} messages={MESSAGES[locale]}>
      <PageBody
        pageState={pageState}
        locale={locale}
        onLocaleChange={setLocale}
        email={email}
        organisationName={organisationName}
        onOrganisationNameChange={setOrganisationName}
        directorName={directorName}
        onDirectorNameChange={setDirectorName}
        password={password}
        onPasswordChange={setPassword}
        fieldErrors={fieldErrors}
        onSubmit={submit}
        submitting={submitting}
      />
    </NextIntlClientProvider>
  );
}

interface PageBodyProps {
  pageState: PageState;
  locale: SupportedLocale;
  onLocaleChange: (locale: SupportedLocale) => void;
  email: string;
  organisationName: string;
  onOrganisationNameChange: (value: string) => void;
  directorName: string;
  onDirectorNameChange: (value: string) => void;
  password: string;
  onPasswordChange: (value: string) => void;
  fieldErrors: Record<string, string>;
  onSubmit: (e: React.FormEvent) => void;
  submitting: boolean;
}

function PageBody({
  pageState, locale, onLocaleChange, email, organisationName, onOrganisationNameChange,
  directorName, onDirectorNameChange, password, onPasswordChange, fieldErrors, onSubmit, submitting,
}: PageBodyProps) {
  const t = useTranslations("register");

  return (
    <main className="mx-auto min-h-screen max-w-lg px-4 py-8">
      <div className="mb-8 flex items-center gap-3">
        <Heart className="h-6 w-6 text-primary dark:text-primary-dark" strokeWidth={2} />
        <h1 className="text-xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
      </div>

      {pageState === "loading" && <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}

      {pageState === "invalid" && (
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("invalidLink")}</p>
      )}

      {pageState === "submitted" && (
        <div className="space-y-4">
          <p className="text-sm text-text dark:text-text-dark">{t("confirmationBody")}</p>
          <a href="/login" className="inline-block text-sm font-medium text-primary-hover dark:text-primary-hover-dark">
            {t("goToLogin")}
          </a>
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

          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("emailLabel")}
            <Input className="mt-2" value={email} disabled readOnly />
          </label>

          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("organisationNameLabel")}
            <Input
              className="mt-2"
              invalid={!!fieldErrors.OrganisationName}
              required
              value={organisationName}
              onChange={(e) => onOrganisationNameChange(e.target.value)}
            />
            {fieldErrors.OrganisationName && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.OrganisationName}</p>}
          </label>

          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("directorNameLabel")}
            <Input
              className="mt-2"
              invalid={!!fieldErrors.DirectorName}
              required
              value={directorName}
              onChange={(e) => onDirectorNameChange(e.target.value)}
            />
            {fieldErrors.DirectorName && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.DirectorName}</p>}
          </label>

          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("passwordLabel")}
            <Input
              type="password"
              className="mt-2"
              invalid={!!fieldErrors.Password}
              required
              value={password}
              onChange={(e) => onPasswordChange(e.target.value)}
            />
            {fieldErrors.Password && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{fieldErrors.Password}</p>}
          </label>

          <Button type="submit" disabled={submitting} className="w-full">
            {submitting ? t("submittingAction") : t("submitAction")}
          </Button>
        </form>
      )}
    </main>
  );
}
