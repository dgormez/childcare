"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { apiClient } from "../../lib/apiClient";

const DOCUMENT_TYPES = ["employment_contract", "amendment", "qualification", "training", "other"] as const;
const ALLOWED_CONTENT_TYPES = ["application/pdf", "image/jpeg", "image/png"];

interface StaffDocumentFormProps {
  staffProfileId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}

/**
 * Director upload of one HR document (spec.md FR-011/FR-011a) — two-step upload (signed URL,
 * then a direct PUT, then confirm) mirrors the health-record attachment flow
 * (children/[id]/page.tsx's uploadHealthRecordAttachment), except the document row here doesn't
 * exist until after the upload, since a dossier can have many documents (unlike a single
 * per-record attachment).
 */
export function StaffDocumentForm({ staffProfileId, open, onOpenChange, onSaved }: StaffDocumentFormProps) {
  const t = useTranslations("staff.dossier.form");
  const [documentType, setDocumentType] = useState<(typeof DOCUMENT_TYPES)[number]>("employment_contract");
  const [title, setTitle] = useState("");
  const [validFrom, setValidFrom] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function reset() {
    setDocumentType("employment_contract");
    setTitle("");
    setValidFrom("");
    setValidUntil("");
    setFile(null);
    setError(null);
  }

  async function save() {
    if (!file) {
      setError(t("fileRequired"));
      return;
    }
    if (!ALLOWED_CONTENT_TYPES.includes(file.type)) {
      setError(t("invalidType"));
      return;
    }

    setSaving(true);
    setError(null);

    const urlResult = await apiClient.POST("/api/staff/{id}/documents/upload-url", {
      params: { path: { id: staffProfileId } },
      body: { contentType: file.type },
    });
    if (!urlResult.response.ok) {
      setSaving(false);
      setError(t("uploadError"));
      return;
    }
    const { objectPath, uploadUrl } = urlResult.data as unknown as { objectPath: string; uploadUrl: string };

    const putResult = await fetch(uploadUrl, { method: "PUT", body: file, headers: { "Content-Type": file.type } });
    if (!putResult.ok) {
      setSaving(false);
      setError(t("uploadError"));
      return;
    }

    const confirmResult = await apiClient.POST("/api/staff/{id}/documents", {
      params: { path: { id: staffProfileId } },
      body: {
        documentType,
        title,
        objectPath,
        validFrom: validFrom || null,
        validUntil: validUntil || null,
      },
    });
    setSaving(false);
    if (!confirmResult.response.ok) {
      setError(t("saveError"));
      return;
    }

    reset();
    onSaved();
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
        </DialogHeader>

        {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}

        <div className="space-y-4">
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("documentTypeLabel")}
            <select
              value={documentType}
              onChange={(e) => setDocumentType(e.target.value as (typeof DOCUMENT_TYPES)[number])}
              className="mt-1 block w-full rounded-lg border-0 bg-surface-soft px-3 py-2 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark"
            >
              {DOCUMENT_TYPES.map((type) => (
                <option key={type} value={type}>
                  {t(`documentTypes.${type}`)}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("titleLabel")}
            <Input value={title} onChange={(e) => setTitle(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("validFromLabel")}
            <Input type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("validUntilLabel")}
            <Input type="date" value={validUntil} onChange={(e) => setValidUntil(e.target.value)} />
          </label>
          <label className="block text-sm font-medium text-text dark:text-text-dark">
            {t("fileLabel")}
            <input
              type="file"
              accept="application/pdf,image/jpeg,image/png"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="mt-1 block w-full text-sm text-text dark:text-text-dark"
            />
          </label>
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            {t("cancel")}
          </Button>
          <Button onClick={save} disabled={saving || !title}>
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
