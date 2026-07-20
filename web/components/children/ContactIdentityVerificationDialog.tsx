"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Textarea } from "../ui/textarea";
import { apiClient } from "../../lib/apiClient";
import type { ChildContactResponse, IdDocumentType } from "../../lib/types";

const DOCUMENT_TYPES: IdDocumentType[] = ["birth_certificate", "kids_id", "eid", "passport", "other"];

interface ContactIdentityVerificationDialogProps {
  contact: ChildContactResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onVerified: () => void;
}

/** Feature 022 US2/US3: verifies (or corrects) a contact's identity, mirroring
 * LinkContactDialog.tsx's modal structure. Keyed by contactId — verification lives on the
 * Contact record itself, independent of which child's tab it was opened from. */
export function ContactIdentityVerificationDialog({ contact, open, onOpenChange, onVerified }: ContactIdentityVerificationDialogProps) {
  const t = useTranslations("children.contacts.identity");
  const [documentType, setDocumentType] = useState<IdDocumentType | "">("");
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !contact) return;
    setDocumentType(contact.idDocumentType ?? "");
    setNote(contact.idDocumentNote ?? "");
    setError(null);
  }, [open, contact]);

  async function submit() {
    if (!contact || !documentType) return;
    setSaving(true);
    setError(null);
    const result = await apiClient.POST("/api/contacts/{id}/identity-verification", {
      params: { path: { id: contact.contactId } },
      body: { documentType, note: note.trim() || null },
    });
    setSaving(false);
    if (!result.response.ok) {
      setError(t("saveError"));
      return;
    }
    onOpenChange(false);
    onVerified();
  }

  const showBothAttribution = !!contact?.idVerifiedAt && contact.firstIdVerifiedAt !== contact.idVerifiedAt;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("dialogTitle")}</DialogTitle>
        </DialogHeader>

        {contact && (
          <div className="space-y-4">
            {showBothAttribution && (
              <div className="rounded-lg bg-surface-soft px-4 py-3 text-sm text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark">
                <p>{t("firstVerifiedLabel")}: {t("verifiedByValue", { email: contact.firstIdVerifiedByEmail ?? "", date: new Date(contact.firstIdVerifiedAt!).toLocaleDateString() })}</p>
              </div>
            )}
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("documentTypeLabel")}
              <select
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value as IdDocumentType)}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                <option value="">{t("documentTypeNone")}</option>
                {DOCUMENT_TYPES.map((dt) => (
                  <option key={dt} value={dt}>{t(`documentType.${dt}`)}</option>
                ))}
              </select>
            </label>
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {t("noteLabel")}
              <Textarea className="mt-2" value={note} onChange={(e) => setNote(e.target.value)} />
            </label>
            {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
          </div>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancelButton")}
          </Button>
          <Button onClick={submit} disabled={saving || !documentType}>
            {t("confirmButton")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
