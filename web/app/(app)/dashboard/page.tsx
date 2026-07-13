"use client";
import { useTranslations } from "next-intl";
import { DueSoonBlock } from "../../../components/health/DueSoonBlock";

// First screen in this app with dashboard-shaped content (feature 013c's due-soon block).
// A future feature adding more widgets extends this page, not a new one.
export default function DashboardPage() {
  const t = useTranslations("dashboard");
  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
      <DueSoonBlock />
    </div>
  );
}
