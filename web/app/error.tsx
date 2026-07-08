"use client";
import { useTranslations } from "next-intl";

export default function Error({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useTranslations("errorBoundary");
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4 dark:bg-background-dark">
      <div className="max-w-sm space-y-4 text-center">
        <h2 className="text-xl font-bold text-text dark:text-text-dark">{t("title")}</h2>
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("body")}</p>
        <button
          onClick={reset}
          className="rounded-lg bg-primary px-6 py-3 font-semibold text-white transition hover:bg-primary-hover"
        >
          {t("retry")}
        </button>
      </div>
    </div>
  );
}
