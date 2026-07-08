"use client";
import { AlertTriangle } from "lucide-react";
import { useTranslations } from "next-intl";

// Catches errors thrown by any page inside the (app) group.
// Renders within the existing app layout so the sidebar stays visible.
export default function AppError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useTranslations("errorBoundary");
  return (
    <div className="flex h-96 items-center justify-center">
      <div className="max-w-sm space-y-3 text-center">
        <AlertTriangle className="mx-auto h-6 w-6 text-danger dark:text-danger-dark" strokeWidth={2} />
        <h2 className="text-lg font-bold text-text dark:text-text-dark">{t("title")}</h2>
        <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("appBody")}</p>
        <button
          onClick={reset}
          className="rounded-lg bg-primary px-6 py-3 text-sm font-semibold text-white transition hover:bg-primary-hover"
        >
          {t("retry")}
        </button>
      </div>
    </div>
  );
}
