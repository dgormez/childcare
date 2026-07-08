"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { login } from "../../../lib/auth";
import { useAuth } from "../../../components/AuthProvider";
import GoogleSignInButton from "../../../components/GoogleSignInButton";

export default function LoginPage() {
  const t = useTranslations("login");
  const router = useRouter();
  const { setSession } = useAuth();
  const [organisationSlug, setOrganisationSlug] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const session = await login(organisationSlug, email, password);
      setSession(session);
      router.replace("/staff");
    } catch (e) {
      const status = (e as { status?: number }).status;
      if (status === 429) setError(t("errorTooManyAttempts"));
      else setError(t("errorInvalidCredentials"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <label htmlFor="organisationSlug" className="block text-sm font-medium text-text dark:text-text-dark mb-1">
          {t("organisationLabel")}
        </label>
        <input
          id="organisationSlug"
          type="text"
          required
          autoComplete="organization"
          value={organisationSlug}
          onChange={(e) => setOrganisationSlug(e.target.value)}
          className="w-full bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4 py-3 text-sm text-text dark:text-text-dark focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder={t("organisationPlaceholder")}
        />
      </div>
      <div>
        <label htmlFor="email" className="block text-sm font-medium text-text dark:text-text-dark mb-1">
          {t("emailLabel")}
        </label>
        <input
          id="email"
          type="email"
          required
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4 py-3 text-sm text-text dark:text-text-dark focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="you@example.com"
        />
      </div>
      <div>
        <label htmlFor="password" className="block text-sm font-medium text-text dark:text-text-dark mb-1">
          {t("passwordLabel")}
        </label>
        <input
          id="password"
          type="password"
          required
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full bg-surface-soft dark:bg-surface-soft-dark rounded-lg px-4 py-3 text-sm text-text dark:text-text-dark focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="••••••••"
        />
      </div>

      {error && <p className="text-sm text-danger" role="alert">{error}</p>}

      <button
        type="submit"
        disabled={loading}
        className="w-full bg-primary text-white font-semibold py-3 rounded-lg transition disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {loading ? t("submitLoading") : t("submit")}
      </button>

      {organisationSlug && (
        <>
          <div className="relative my-2">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-border dark:border-border-dark" />
            </div>
            <div className="relative flex justify-center text-xs text-text-soft dark:text-text-soft-dark">
              <span className="bg-background dark:bg-background-dark px-2">{t("orDivider")}</span>
            </div>
          </div>

          <GoogleSignInButton organisationSlug={organisationSlug} />
        </>
      )}
    </form>
  );
}
