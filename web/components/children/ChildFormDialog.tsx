"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";
import type { ChildResponse } from "../../lib/types";

export interface ChildFormValues {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  gender: string | null;
  nationality: string | null;
  allergiesDescription: string | null;
  allergySeverity: string | null;
  medicalConditions: string | null;
  dietaryRestrictions: string | null;
  gpName: string | null;
  gpPhone: string | null;
  pediatricianName: string | null;
  pediatricianPhone: string | null;
  healthInsuranceNumber: string | null;
  kindcode: string | null;
}

interface ChildFormDialogProps {
  open: boolean;
  mode: "create" | "edit";
  child: ChildResponse | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: ChildFormValues) => Promise<void>;
  saving: boolean;
  error?: string | null;
}

const GENDERS = ["Male", "Female", "Other"] as const;
const ALLERGY_SEVERITIES = ["Mild", "Moderate", "Severe"] as const;

const emptyValues: ChildFormValues = {
  firstName: "",
  lastName: "",
  dateOfBirth: "",
  gender: null,
  nationality: null,
  allergiesDescription: null,
  allergySeverity: null,
  medicalConditions: null,
  dietaryRestrictions: null,
  gpName: null,
  gpPhone: null,
  pediatricianName: null,
  pediatricianPhone: null,
  healthInsuranceNumber: null,
  kindcode: null,
};

/** Director create/edit form for a child's general profile and medical contacts (006a US1/US2).
 * Medical/contact fields never block save (FR-002) — only first/last name and date of birth are
 * required. GP (huisarts) and pediatrician (kinderarts) are independent optional contacts —
 * clearing one never touches the other (FR-006/FR-007). */
export function ChildFormDialog({ open, mode, child, onOpenChange, onSubmit, saving, error }: ChildFormDialogProps) {
  const t = useTranslations("children.form");
  const [values, setValues] = useState<ChildFormValues>(emptyValues);
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTouched(false);
    setValues(
      child
        ? {
            firstName: child.firstName,
            lastName: child.lastName,
            dateOfBirth: child.dateOfBirth,
            gender: child.gender,
            nationality: child.nationality,
            allergiesDescription: child.allergiesDescription,
            allergySeverity: child.allergySeverity,
            medicalConditions: child.medicalConditions,
            dietaryRestrictions: child.dietaryRestrictions,
            gpName: child.gpName,
            gpPhone: child.gpPhone,
            pediatricianName: child.pediatricianName,
            pediatricianPhone: child.pediatricianPhone,
            healthInsuranceNumber: child.healthInsuranceNumber,
            kindcode: child.kindcode,
          }
        : emptyValues,
    );
  }, [open, child]);

  function set<K extends keyof ChildFormValues>(key: K, value: ChildFormValues[K]) {
    setValues((prev) => ({ ...prev, [key]: value }));
  }

  const firstNameInvalid = touched && !values.firstName.trim();
  const lastNameInvalid = touched && !values.lastName.trim();
  const dateOfBirthInvalid = touched && !values.dateOfBirth;
  const canSubmit = !!values.firstName.trim() && !!values.lastName.trim() && !!values.dateOfBirth;

  function handleSubmit() {
    setTouched(true);
    if (!canSubmit) return;
    onSubmit({
      ...values,
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      nationality: values.nationality?.trim() || null,
      allergiesDescription: values.allergiesDescription?.trim() || null,
      medicalConditions: values.medicalConditions?.trim() || null,
      dietaryRestrictions: values.dietaryRestrictions?.trim() || null,
      gpName: values.gpName?.trim() || null,
      gpPhone: values.gpPhone?.trim() || null,
      pediatricianName: values.pediatricianName?.trim() || null,
      pediatricianPhone: values.pediatricianPhone?.trim() || null,
      healthInsuranceNumber: values.healthInsuranceNumber?.trim() || null,
      kindcode: values.kindcode?.trim() || null,
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{mode === "create" ? t("createTitle") : t("editTitle")}</DialogTitle>
        </DialogHeader>

        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("firstNameLabel")}
              <Input
                className="mt-2"
                invalid={firstNameInvalid}
                value={values.firstName}
                onChange={(e) => set("firstName", e.target.value)}
              />
              {firstNameInvalid && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{t("firstNameRequired")}</p>}
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("lastNameLabel")}
              <Input
                className="mt-2"
                invalid={lastNameInvalid}
                value={values.lastName}
                onChange={(e) => set("lastName", e.target.value)}
              />
              {lastNameInvalid && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{t("lastNameRequired")}</p>}
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("dateOfBirthLabel")}
              <Input
                type="date"
                className="mt-2"
                invalid={dateOfBirthInvalid}
                value={values.dateOfBirth}
                onChange={(e) => set("dateOfBirth", e.target.value)}
              />
              {dateOfBirthInvalid && <p className="mt-1 text-xs text-danger dark:text-danger-dark">{t("dateOfBirthRequired")}</p>}
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("genderLabel")}
              <select
                value={values.gender ?? ""}
                onChange={(e) => set("gender", e.target.value || null)}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="">{t("genderNone")}</option>
                {GENDERS.map((g) => (
                  <option key={g} value={g}>{t(`gender.${g.toLowerCase()}`)}</option>
                ))}
              </select>
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("nationalityLabel")}
              <Input className="mt-2" value={values.nationality ?? ""} onChange={(e) => set("nationality", e.target.value || null)} />
            </label>
          </div>

          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-text dark:text-text-dark">{t("medicalSectionTitle")}</h3>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("allergiesDescriptionLabel")}
              <Textarea className="mt-2" value={values.allergiesDescription ?? ""} onChange={(e) => set("allergiesDescription", e.target.value || null)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("allergySeverityLabel")}
              <select
                value={values.allergySeverity ?? ""}
                onChange={(e) => set("allergySeverity", e.target.value || null)}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="">{t("allergySeverityNone")}</option>
                {ALLERGY_SEVERITIES.map((s) => (
                  <option key={s} value={s}>{t(`allergySeverity.${s.toLowerCase()}`)}</option>
                ))}
              </select>
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("medicalConditionsLabel")}
              <Textarea className="mt-2" value={values.medicalConditions ?? ""} onChange={(e) => set("medicalConditions", e.target.value || null)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("dietaryRestrictionsLabel")}
              <Textarea className="mt-2" value={values.dietaryRestrictions ?? ""} onChange={(e) => set("dietaryRestrictions", e.target.value || null)} />
            </label>
          </div>

          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-text dark:text-text-dark">{t("contactsSectionTitle")}</h3>
            <div className="grid grid-cols-2 gap-4">
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("gpNameLabel")}
                <Input className="mt-2" value={values.gpName ?? ""} onChange={(e) => set("gpName", e.target.value || null)} />
              </label>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("gpPhoneLabel")}
                <Input className="mt-2" value={values.gpPhone ?? ""} onChange={(e) => set("gpPhone", e.target.value || null)} />
              </label>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("pediatricianNameLabel")}
                <Input className="mt-2" value={values.pediatricianName ?? ""} onChange={(e) => set("pediatricianName", e.target.value || null)} />
              </label>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("pediatricianPhoneLabel")}
                <Input className="mt-2" value={values.pediatricianPhone ?? ""} onChange={(e) => set("pediatricianPhone", e.target.value || null)} />
              </label>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("healthInsuranceNumberLabel")}
              <Input className="mt-2" value={values.healthInsuranceNumber ?? ""} onChange={(e) => set("healthInsuranceNumber", e.target.value || null)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("kindcodeLabel")}
              <Input className="mt-2" value={values.kindcode ?? ""} onChange={(e) => set("kindcode", e.target.value || null)} />
            </label>
          </div>

          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("cancel")}</Button>
          <Button disabled={saving} onClick={handleSubmit}>{t("save")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
