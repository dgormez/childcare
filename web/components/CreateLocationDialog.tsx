"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";

export interface CreateLocationValues {
  name: string;
  address: string;
  phone: string;
  email: string;
  maxCapacity: number;
}

interface CreateLocationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: CreateLocationValues) => Promise<void>;
  saving: boolean;
  error?: string | null;
}

const emptyValues: CreateLocationValues = { name: "", address: "", phone: "", email: "", maxCapacity: 1 };

/** Backend has supported location creation (POST /api/locations) since feature 004 — this is
 * the first web UI to expose it; adding one doesn't happen often, but there was previously no
 * way to do it from director web at all. */
export function CreateLocationDialog({ open, onOpenChange, onSubmit, saving, error }: CreateLocationDialogProps) {
  const t = useTranslations("locations.general");
  const tc = useTranslations("locations");
  const [values, setValues] = useState<CreateLocationValues>(emptyValues);
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (open) {
      setValues(emptyValues);
      setTouched(false);
    }
  }, [open]);

  function set<K extends keyof CreateLocationValues>(key: K, value: CreateLocationValues[K]) {
    setValues((prev) => ({ ...prev, [key]: value }));
  }

  const canSubmit = !!values.name.trim() && !!values.address.trim() && !!values.phone.trim() && !!values.email.trim() && values.maxCapacity > 0;

  function handleSubmit() {
    setTouched(true);
    if (!canSubmit) return;
    onSubmit({
      name: values.name.trim(),
      address: values.address.trim(),
      phone: values.phone.trim(),
      email: values.email.trim(),
      maxCapacity: values.maxCapacity,
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{tc("createTitle")}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("nameLabel")}
            <Input className="mt-2" invalid={touched && !values.name.trim()} value={values.name} onChange={(e) => set("name", e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("addressLabel")}
            <Input className="mt-2" invalid={touched && !values.address.trim()} value={values.address} onChange={(e) => set("address", e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("phoneLabel")}
            <Input className="mt-2" invalid={touched && !values.phone.trim()} value={values.phone} onChange={(e) => set("phone", e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("emailLabel")}
            <Input className="mt-2" type="email" invalid={touched && !values.email.trim()} value={values.email} onChange={(e) => set("email", e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("capacityLabel")}
            <Input
              className="mt-2 max-w-[8rem] tabular-nums"
              type="number"
              min={1}
              invalid={touched && !(values.maxCapacity > 0)}
              value={values.maxCapacity}
              onChange={(e) => set("maxCapacity", Number(e.target.value) || 0)}
            />
          </label>
          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{tc("cancel")}</Button>
          <Button disabled={saving} onClick={handleSubmit}>{tc("createButton")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
