import { Construction } from "lucide-react";
import { useTranslations } from "next-intl";

/** Spec Edge Cases: a director who navigates directly to a not-yet-built section's URL
 * (bypassing the sidebar's disabled entry) sees this instead of a broken route or raw 404. */
export function NotYetAvailable() {
  const t = useTranslations("sidebar");
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-24 text-center">
      <Construction className="h-6 w-6 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
      <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("notYetAvailable")}</p>
    </div>
  );
}
