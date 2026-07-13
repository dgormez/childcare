"use client";
import { useId, useState } from "react";
import { useTranslations } from "next-intl";
import { User } from "lucide-react";
import { Button } from "../ui/button";
import type { ChildResponse } from "../../lib/types";

const ALLOWED_PHOTO_TYPES = ["image/jpeg", "image/png"];

interface ChildProfileTabProps {
  child: ChildResponse;
  onEdit: () => void;
  onPhotoUpload: (file: File) => Promise<boolean>;
}

/** Read-only display of a child's general profile and medical contacts (006a US1), with an
 * "Edit" action that hands off to the shared ChildFormDialog in edit mode (US2). GP and
 * pediatrician contact are shown as two visually distinct rows — never merged or implied to be
 * the same contact (FR-006/FR-008). The profile photo control mirrors
 * HealthRecordAttachmentControl.tsx's keyboard-operable label+sr-only-input pattern (FR-002). */
export function ChildProfileTab({ child, onEdit, onPhotoUpload }: ChildProfileTabProps) {
  const t = useTranslations("children.form");
  const tp = useTranslations("children.profile");
  const photoInputId = useId();
  const [photoStatus, setPhotoStatus] = useState<"idle" | "uploading" | "error">("idle");
  const [photoAnnouncement, setPhotoAnnouncement] = useState("");

  async function handlePhotoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    if (!ALLOWED_PHOTO_TYPES.includes(file.type)) {
      setPhotoStatus("error");
      setPhotoAnnouncement(tp("photoInvalidType"));
      return;
    }

    setPhotoStatus("uploading");
    setPhotoAnnouncement(tp("photoUploading"));
    const success = await onPhotoUpload(file);
    if (success) {
      setPhotoStatus("idle");
      setPhotoAnnouncement(tp("photoUploadSuccess"));
    } else {
      setPhotoStatus("error");
      setPhotoAnnouncement(tp("photoUploadError"));
    }
  }

  const rows: { label: string; value: string | null }[] = [
    { label: t("dateOfBirthLabel"), value: child.dateOfBirth },
    { label: t("genderLabel"), value: child.gender ? t(`gender.${child.gender.toLowerCase()}`) : null },
    { label: t("nationalityLabel"), value: child.nationality },
    { label: t("allergiesDescriptionLabel"), value: child.allergiesDescription },
    { label: t("allergySeverityLabel"), value: child.allergySeverity ? t(`allergySeverity.${child.allergySeverity.toLowerCase()}`) : null },
    { label: t("medicalConditionsLabel"), value: child.medicalConditions },
    { label: t("dietaryRestrictionsLabel"), value: child.dietaryRestrictions },
    { label: t("gpNameLabel"), value: child.gpName },
    { label: t("gpPhoneLabel"), value: child.gpPhone },
    { label: t("pediatricianNameLabel"), value: child.pediatricianName },
    { label: t("pediatricianPhoneLabel"), value: child.pediatricianPhone },
    { label: t("healthInsuranceNumberLabel"), value: child.healthInsuranceNumber },
    { label: t("kindcodeLabel"), value: child.kindcode },
  ];

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div className="flex items-center gap-4">
          {child.photoDownloadUrl ? (
            // eslint-disable-next-line @next/next/no-img-element -- signed GCS URL, not a static asset
            <img
              src={child.photoDownloadUrl}
              alt=""
              className="h-16 w-16 rounded-full object-cover"
            />
          ) : (
            <div className="flex h-16 w-16 items-center justify-center rounded-full bg-surface-soft dark:bg-surface-soft-dark">
              <User className="h-6 w-6 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
            </div>
          )}
          <div>
            <label
              htmlFor={photoInputId}
              className="cursor-pointer text-sm text-primary-hover hover:underline dark:text-primary-hover-dark"
            >
              {child.photoDownloadUrl ? tp("photoReplace") : tp("photoAdd")}
            </label>
            <input
              id={photoInputId}
              type="file"
              accept="image/jpeg,image/png"
              className="sr-only"
              disabled={photoStatus === "uploading"}
              onChange={handlePhotoChange}
            />
            <span role="status" aria-live="polite" className="sr-only">{photoAnnouncement}</span>
            {photoStatus === "error" && (
              <p className="text-xs text-danger dark:text-danger-dark">{photoAnnouncement}</p>
            )}
          </div>
        </div>
        <Button size="sm" onClick={onEdit}>{tp("editButton")}</Button>
      </div>

      <dl className="divide-y divide-border dark:divide-border-dark">
        {rows.map((row) => (
          <div key={row.label} className="grid grid-cols-3 gap-4 py-3">
            <dt className="text-sm text-text-soft dark:text-text-soft-dark">{row.label}</dt>
            <dd className="col-span-2 text-sm text-text dark:text-text-dark">{row.value || tp("notSet")}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
