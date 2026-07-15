"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { ArrowDown, ArrowUp } from "lucide-react";
import { Button } from "./ui/button";
import { ConfirmDialog } from "./ConfirmDialog";
import { apiClient } from "../lib/apiClient";
import type { ApiErrorBody, LocationResponse } from "../lib/types";

interface MenuVariantSettingsFormProps {
  location: LocationResponse;
  onSaved: (updated: LocationResponse) => void;
}

const ALL_DIETARY_TYPES = ["halal", "kosher", "vegetarian", "vegan", "gluten_free"] as const;

/** Feature 013j FR-001/FR-002/FR-014. Mirrors ReservationSettingsForm.tsx's structure exactly,
 * including its "confirm despite a real consequence" ConfirmDialog pattern (013f). */
export function MenuVariantSettingsForm({ location, onSaved }: MenuVariantSettingsFormProps) {
  const t = useTranslations("locations.menuVariants");
  const tDiet = useTranslations("mealPreferenceRequests.dietaryType");
  const [order, setOrder] = useState<string[]>(location.menuVariantPriorityOrder);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");
  const [removalWarning, setRemovalWarning] = useState<string[] | null>(null);

  function toggle(dietaryType: string, enabled: boolean) {
    setOrder((current) => (enabled ? [...current, dietaryType] : current.filter((v) => v !== dietaryType)));
  }

  function move(index: number, direction: -1 | 1) {
    setOrder((current) => {
      const next = [...current];
      const target = index + direction;
      if (target < 0 || target >= next.length) return current;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  async function save(confirmDespiteRemovingPublished: boolean) {
    setSaving(true);
    setNotice("");
    const result = await apiClient.PUT("/api/locations/{id}/menu-variant-settings", {
      params: { path: { id: location.id } },
      body: { menuVariantPriorityOrder: order, confirmDespiteRemovingPublished },
    });
    setSaving(false);

    if (!result.response.ok) {
      const error = (result.error ?? {}) as ApiErrorBody;
      if (error.errorKey === "errors.location.menu_variant_settings.removing_published_warning" && error.variants) {
        setRemovalWarning(error.variants);
        return;
      }
      setNotice(t("saveError"));
      return;
    }

    setRemovalWarning(null);
    setNotice(t("saveSuccess"));
    onSaved(result.data as unknown as LocationResponse);
  }

  return (
    <div className="max-w-xl space-y-6">
      <p className="text-sm text-text-soft dark:text-text-soft-dark">{t("description")}</p>

      <div className="space-y-2">
        {ALL_DIETARY_TYPES.map((dietaryType) => {
          const enabled = order.includes(dietaryType);
          const index = order.indexOf(dietaryType);
          return (
            <div
              key={dietaryType}
              className="flex items-center justify-between gap-4 rounded-lg bg-surface-soft px-4 py-3 dark:bg-surface-soft-dark"
            >
              <label className="flex items-center gap-3 text-sm font-medium text-text dark:text-text-dark">
                <input
                  type="checkbox"
                  checked={enabled}
                  onChange={(e) => toggle(dietaryType, e.target.checked)}
                  className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary"
                />
                {tDiet(dietaryType)}
              </label>
              {enabled && (
                <div className="flex items-center gap-2">
                  <span className="text-xs tabular-nums text-text-soft dark:text-text-soft-dark">{index + 1}</span>
                  <Button
                    variant="secondary"
                    size="sm"
                    aria-label={t("moveUp")}
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                  >
                    <ArrowUp className="h-3 w-3" strokeWidth={2} />
                  </Button>
                  <Button
                    variant="secondary"
                    size="sm"
                    aria-label={t("moveDown")}
                    disabled={index === order.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    <ArrowDown className="h-3 w-3" strokeWidth={2} />
                  </Button>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {notice && <p className="text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      <Button onClick={() => save(false)} disabled={saving}>
        {t("saveButton")}
      </Button>

      <ConfirmDialog
        open={removalWarning !== null}
        onOpenChange={(open) => !open && setRemovalWarning(null)}
        title={t("warningTitle")}
        description={
          removalWarning ? `${t("warningBody")} (${removalWarning.map((v) => tDiet(v)).join(", ")})` : ""
        }
        confirmLabel={t("warningConfirm")}
        cancelLabel={t("warningCancel")}
        confirming={saving}
        onConfirm={() => save(true)}
      />
    </div>
  );
}
