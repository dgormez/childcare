"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";
import type { HealthRecordResponse, HealthRecordType } from "../../lib/types";

export interface HealthRecordFormValues {
  recordType: HealthRecordType;
  title: string;
  description: string;
  validFrom: string | null;
  validUntil: string | null;
}

interface HealthRecordFormProps {
  open: boolean;
  record: HealthRecordResponse | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: HealthRecordFormValues) => Promise<void>;
  saving: boolean;
  error?: string | null;
}

const RECORD_TYPES: HealthRecordType[] = ["allergy", "chronic_condition", "medication_standing", "doctor_note", "other"];

/** Director create/edit form for one health record (spec.md FR-004/FR-005, US2). Attachment
 * upload is a separate, subsequent action (contracts/vaccine-health-records-api.md) — a failed
 * upload must never block saving the record itself (FR-007). */
export function HealthRecordForm({ open, record, onOpenChange, onSubmit, saving, error }: HealthRecordFormProps) {
  const t = useTranslations("children.health.records.form");
  const [recordType, setRecordType] = useState<HealthRecordType>("allergy");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [validFrom, setValidFrom] = useState("");
  const [validUntil, setValidUntil] = useState("");

  useEffect(() => {
    if (!open) return;
    setRecordType(record?.recordType ?? "allergy");
    setTitle(record?.title ?? "");
    setDescription(record?.description ?? "");
    setValidFrom(record?.validFrom ?? "");
    setValidUntil(record?.validUntil ?? "");
  }, [open, record]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{record ? t("editTitle") : t("addTitle")}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("recordTypeLabel")}
            <select
              value={recordType}
              onChange={(e) => setRecordType(e.target.value as HealthRecordType)}
              className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              {RECORD_TYPES.map((type) => (
                <option key={type} value={type}>{t(`recordType.${type}`)}</option>
              ))}
            </select>
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("titleLabel")}
            <Input className="mt-2" value={title} onChange={(e) => setTitle(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("descriptionLabel")}
            <Textarea className="mt-2" value={description} onChange={(e) => setDescription(e.target.value)} />
          </label>
          <div className="flex gap-4">
            <label className="block flex-1 text-sm font-medium text-text dark:text-text-dark">
              {t("validFromLabel")}
              <Input className="mt-2" type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} />
            </label>
            <label className="block flex-1 text-sm font-medium text-text dark:text-text-dark">
              {t("validUntilLabel")}
              <Input className="mt-2" type="date" value={validUntil} onChange={(e) => setValidUntil(e.target.value)} />
            </label>
          </div>
          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>{t("cancel")}</Button>
          <Button
            disabled={saving || !title.trim() || !description.trim()}
            onClick={() =>
              onSubmit({
                recordType,
                title: title.trim(),
                description: description.trim(),
                validFrom: validFrom || null,
                validUntil: validUntil || null,
              })
            }
          >
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
