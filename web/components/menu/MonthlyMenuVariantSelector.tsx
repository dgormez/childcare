"use client";
import { useTranslations } from "next-intl";

interface MonthlyMenuVariantSelectorProps {
  availableVariants: string[];
  value: string | null;
  onChange: (variant: string | null) => void;
}

/** Feature 013j FR-003/FR-004. Only rendered when a location has enabled variants
 * (locations.menuVariantPriorityOrder) — otherwise the base menu is the only option. */
export function MonthlyMenuVariantSelector({ availableVariants, value, onChange }: MonthlyMenuVariantSelectorProps) {
  const t = useTranslations("menu.variantSelector");
  const tDiet = useTranslations("mealPreferenceRequests.dietaryType");

  if (availableVariants.length === 0) return null;

  return (
    <select
      value={value ?? ""}
      onChange={(e) => onChange(e.target.value || null)}
      aria-label={t("label")}
      className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
    >
      <option value="">{t("base")}</option>
      {availableVariants.map((variant) => (
        <option key={variant} value={variant}>
          {tDiet(variant)}
        </option>
      ))}
    </select>
  );
}
