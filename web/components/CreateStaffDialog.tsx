"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { LocationResponse } from "../lib/types";

export interface CreateStaffValues {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  role: "Director" | "Staff";
  qualificationLevel: string | null;
  locationIds: string[];
}

interface CreateStaffDialogProps {
  open: boolean;
  locations: LocationResponse[];
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: CreateStaffValues) => Promise<void>;
  saving: boolean;
  error?: string | null;
}

const QUALIFICATION_LEVELS = ["QualifiedCaregiver", "Auxiliary", "StudentVolunteer"] as const;

const emptyValues: CreateStaffValues = {
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  role: "Staff",
  qualificationLevel: null,
  locationIds: [],
};

/** Backend has supported staff creation (POST /api/staff) and per-location assignment (PUT
 * /api/staff/{id}/locations/{locationId}) since feature 005 — this is the first web UI to expose
 * either. Creates the profile first (which sends the caregiver an invitation email), then
 * assigns every checked location. */
export function CreateStaffDialog({ open, locations, onOpenChange, onSubmit, saving, error }: CreateStaffDialogProps) {
  const t = useTranslations("staff.create");
  const tr = useTranslations("staff.role");
  const [values, setValues] = useState<CreateStaffValues>(emptyValues);
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (open) {
      setValues(emptyValues);
      setTouched(false);
    }
  }, [open]);

  function set<K extends keyof CreateStaffValues>(key: K, value: CreateStaffValues[K]) {
    setValues((prev) => ({ ...prev, [key]: value }));
  }

  function toggleLocation(locationId: string) {
    setValues((prev) => ({
      ...prev,
      locationIds: prev.locationIds.includes(locationId)
        ? prev.locationIds.filter((id) => id !== locationId)
        : [...prev.locationIds, locationId],
    }));
  }

  const canSubmit = !!values.firstName.trim() && !!values.lastName.trim() && !!values.email.trim() && !!values.phone.trim();

  function handleSubmit() {
    setTouched(true);
    if (!canSubmit) return;
    onSubmit({
      ...values,
      firstName: values.firstName.trim(),
      lastName: values.lastName.trim(),
      email: values.email.trim(),
      phone: values.phone.trim(),
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("firstNameLabel")}
              <Input className="mt-2" invalid={touched && !values.firstName.trim()} value={values.firstName} onChange={(e) => set("firstName", e.target.value)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("lastNameLabel")}
              <Input className="mt-2" invalid={touched && !values.lastName.trim()} value={values.lastName} onChange={(e) => set("lastName", e.target.value)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("emailLabel")}
              <Input className="mt-2" type="email" invalid={touched && !values.email.trim()} value={values.email} onChange={(e) => set("email", e.target.value)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("phoneLabel")}
              <Input className="mt-2" invalid={touched && !values.phone.trim()} value={values.phone} onChange={(e) => set("phone", e.target.value)} />
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("roleLabel")}
              <select
                value={values.role}
                onChange={(e) => set("role", e.target.value as "Director" | "Staff")}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="Staff">{tr("staff")}</option>
                <option value="Director">{tr("director")}</option>
              </select>
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("qualificationLabel")}
              <select
                value={values.qualificationLevel ?? ""}
                onChange={(e) => set("qualificationLevel", e.target.value || null)}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="">{t("qualificationNone")}</option>
                {QUALIFICATION_LEVELS.map((level) => (
                  <option key={level} value={level}>{t(`qualification.${level}`)}</option>
                ))}
              </select>
            </label>
          </div>

          {locations.length > 0 && (
            <div className="space-y-2">
              <p className="text-sm font-medium text-text dark:text-text-dark">{t("locationsLabel")}</p>
              <div className="space-y-1">
                {locations.map((location) => (
                  <label key={location.id} className="flex items-center gap-2 text-sm text-text dark:text-text-dark">
                    <input
                      type="checkbox"
                      checked={values.locationIds.includes(location.id)}
                      onChange={() => toggleLocation(location.id)}
                      className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary"
                    />
                    {location.name}
                  </label>
                ))}
              </div>
            </div>
          )}

          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("cancel")}</Button>
          <Button disabled={saving} onClick={handleSubmit}>{t("createButton")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
