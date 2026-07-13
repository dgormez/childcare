"use client";
import { useId, useState } from "react";
import { useTranslations } from "next-intl";
import { Paperclip } from "lucide-react";

const ALLOWED_CONTENT_TYPES = ["application/pdf", "image/jpeg", "image/png"];
const MAX_SIZE_BYTES = 10 * 1024 * 1024; // 10MB, spec.md FR-006

interface HealthRecordAttachmentControlProps {
  attachmentDownloadUrl: string | null;
  onUpload: (file: File) => Promise<boolean>;
}

/**
 * Keyboard-operable attachment upload control (spec.md FR-020) — a native <input type="file">
 * associated with a visible <label> is keyboard/screen-reader operable by construction; progress
 * and outcome are additionally announced via an aria-live region rather than a visual-only
 * spinner or color change.
 */
export function HealthRecordAttachmentControl({ attachmentDownloadUrl, onUpload }: HealthRecordAttachmentControlProps) {
  const t = useTranslations("children.health.records.attachment");
  const inputId = useId();
  const [status, setStatus] = useState<"idle" | "uploading" | "error">("idle");
  const [announcement, setAnnouncement] = useState("");

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    if (!ALLOWED_CONTENT_TYPES.includes(file.type)) {
      setStatus("error");
      setAnnouncement(t("invalidType"));
      return;
    }
    if (file.size > MAX_SIZE_BYTES) {
      setStatus("error");
      setAnnouncement(t("tooLarge"));
      return;
    }

    setStatus("uploading");
    setAnnouncement(t("uploading"));
    const success = await onUpload(file);
    if (success) {
      setStatus("idle");
      setAnnouncement(t("uploadSuccess"));
    } else {
      setStatus("error");
      setAnnouncement(t("uploadError"));
    }
  }

  return (
    <div className="flex items-center gap-2">
      {attachmentDownloadUrl && (
        <a
          href={attachmentDownloadUrl}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center gap-1 text-sm text-primary-hover hover:underline dark:text-primary-hover-dark"
        >
          <Paperclip className="h-3 w-3" strokeWidth={2} />
          {t("viewAttachment")}
        </a>
      )}
      <label
        htmlFor={inputId}
        className="cursor-pointer text-sm text-primary-hover hover:underline dark:text-primary-hover-dark"
      >
        {attachmentDownloadUrl ? t("replaceAttachment") : t("addAttachment")}
      </label>
      <input
        id={inputId}
        type="file"
        accept="application/pdf,image/jpeg,image/png"
        className="sr-only"
        disabled={status === "uploading"}
        onChange={handleFileChange}
      />
      <span role="status" aria-live="polite" className="sr-only">{announcement}</span>
      {status === "error" && <span className="text-xs text-danger dark:text-danger-dark">{announcement}</span>}
    </div>
  );
}
