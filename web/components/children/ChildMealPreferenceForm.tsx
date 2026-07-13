"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "../../lib/apiClient";
import { Button } from "../ui/button";
import { Textarea } from "../ui/textarea";
import type { MealPreferenceResponse, MealTexture, MealPortionSize } from "../../lib/types";

const TEXTURES: MealTexture[] = ["pureed", "mixed", "pieces", "normal"];
const PORTION_SIZES: MealPortionSize[] = ["small", "normal", "large"];
const DIETARY_TYPES = ["halal", "kosher", "vegetarian", "vegan", "gluten_free"];

interface ChildMealPreferenceFormProps {
  childId: string;
}

/** Self-contained meal-preference editor on the child's Profiel tab (feature 013d US3) —
 * fetches current values via the additive GET (contracts/meal-list-api.md), submits only
 * changed fields on save (partial-upsert semantics, data-model.md). Independent of
 * ChildFormDialog/CreateChildCommand — a separate table and endpoint, per spec.md. */
export function ChildMealPreferenceForm({ childId }: ChildMealPreferenceFormProps) {
  const t = useTranslations("mealList");
  const [preference, setPreference] = useState<MealPreferenceResponse | null>(null);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [texture, setTexture] = useState<MealTexture>("normal");
  const [dietaryType, setDietaryType] = useState<string[]>([]);
  const [portionSize, setPortionSize] = useState<MealPortionSize>("normal");
  const [additionalNotes, setAdditionalNotes] = useState("");

  useEffect(() => {
    apiClient.GET("/api/children/{childId}/meal-preferences", { params: { path: { childId } } }).then((result) => {
      if (!result.response.ok) return;
      const data = result.data as unknown as MealPreferenceResponse;
      setPreference(data);
      setTexture(data.texture);
      setDietaryType(data.dietaryType);
      setPortionSize(data.portionSize);
      setAdditionalNotes(data.additionalNotes ?? "");
    });
  }, [childId]);

  function toggleDietaryType(value: string) {
    setDietaryType((current) => (current.includes(value) ? current.filter((d) => d !== value) : [...current, value]));
  }

  async function handleSave() {
    setSaving(true);
    setError(null);
    const result = await apiClient.PUT("/api/children/{childId}/meal-preferences", {
      params: { path: { childId } },
      body: { texture, dietaryType, portionSize, additionalNotes: additionalNotes || null },
    });
    setSaving(false);
    if (!result.response.ok) {
      setError(t("preferenceForm.saveError"));
      return;
    }
    const data = result.data as unknown as MealPreferenceResponse;
    setPreference(data);
    setEditing(false);
  }

  function handleCancel() {
    if (preference) {
      setTexture(preference.texture);
      setDietaryType(preference.dietaryType);
      setPortionSize(preference.portionSize);
      setAdditionalNotes(preference.additionalNotes ?? "");
    }
    setError(null);
    setEditing(false);
  }

  return (
    <div className="mt-6 border-t border-border pt-6 dark:border-border-dark">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-text-soft dark:text-text-soft-dark">
          {t("preferenceForm.sectionTitle")}
        </h2>
        {!editing && <Button size="sm" onClick={() => setEditing(true)}>{t("preferenceForm.editButton")}</Button>}
      </div>

      {!editing && preference && (
        <dl className="divide-y divide-border dark:divide-border-dark">
          <div className="grid grid-cols-3 gap-4 py-3">
            <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("columnTexture")}</dt>
            <dd className="col-span-2 text-sm text-text dark:text-text-dark">{t(`texture.${preference.texture}`)}</dd>
          </div>
          <div className="grid grid-cols-3 gap-4 py-3">
            <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("columnDietaryType")}</dt>
            <dd className="col-span-2 text-sm text-text dark:text-text-dark">
              {preference.dietaryType.length > 0 ? preference.dietaryType.map((d) => t(`dietaryType.${d}`)).join(", ") : "—"}
            </dd>
          </div>
          <div className="grid grid-cols-3 gap-4 py-3">
            <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("columnPortionSize")}</dt>
            <dd className="col-span-2 text-sm text-text dark:text-text-dark">{t(`portionSize.${preference.portionSize}`)}</dd>
          </div>
          <div className="grid grid-cols-3 gap-4 py-3">
            <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("preferenceForm.notesLabel")}</dt>
            <dd className="col-span-2 text-sm text-text dark:text-text-dark">{preference.additionalNotes || "—"}</dd>
          </div>
        </dl>
      )}

      {editing && (
        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("columnTexture")}</label>
            <select
              value={texture}
              onChange={(e) => setTexture(e.target.value as MealTexture)}
              className="h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              {TEXTURES.map((value) => (
                <option key={value} value={value}>{t(`texture.${value}`)}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("columnDietaryType")}</label>
            <div className="flex flex-wrap gap-3">
              {DIETARY_TYPES.map((value) => (
                <label key={value} className="flex items-center gap-2 text-sm text-text dark:text-text-dark">
                  <input
                    type="checkbox"
                    checked={dietaryType.includes(value)}
                    onChange={() => toggleDietaryType(value)}
                    className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
                  />
                  {t(`dietaryType.${value}`)}
                </label>
              ))}
            </div>
          </div>

          <div>
            <label className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("columnPortionSize")}</label>
            <select
              value={portionSize}
              onChange={(e) => setPortionSize(e.target.value as MealPortionSize)}
              className="h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              {PORTION_SIZES.map((value) => (
                <option key={value} value={value}>{t(`portionSize.${value}`)}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm text-text-soft dark:text-text-soft-dark">{t("preferenceForm.notesLabel")}</label>
            <Textarea value={additionalNotes} onChange={(e) => setAdditionalNotes(e.target.value)} maxLength={2000} rows={3} />
          </div>

          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}

          <div className="flex gap-2">
            <Button onClick={handleSave} disabled={saving}>{t("preferenceForm.saveButton")}</Button>
            <Button variant="secondary" onClick={handleCancel} disabled={saving}>{t("preferenceForm.cancelButton")}</Button>
          </div>
        </div>
      )}
    </div>
  );
}
