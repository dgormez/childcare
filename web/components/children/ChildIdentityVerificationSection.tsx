"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { CheckCircle, Clock } from "lucide-react";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";
import { Badge } from "../ui/badge";
import type { ChildResponse, IdDocumentType } from "../../lib/types";

const DOCUMENT_TYPES: IdDocumentType[] = ["birth_certificate", "kids_id", "eid", "passport", "other"];

interface ChildIdentityVerificationSectionProps {
  child: ChildResponse;
  onVerify: (documentType: IdDocumentType, note: string | null) => Promise<boolean>;
  onSetNrn: (nrn: string) => Promise<boolean>;
}

/** Feature 022 US1/US3/US5: the "Identiteit bevestigen" section — read-only display of a
 * verified child's identity, a form to (re-)confirm it (FR-001/FR-003/FR-005), both
 * attribution pairs when a correction has happened (FR-006a), and the NRN entry field
 * (FR-009/FR-012). Mirrors ChildProfileTab.tsx's controlled-component style. */
export function ChildIdentityVerificationSection({ child, onVerify, onSetNrn }: ChildIdentityVerificationSectionProps) {
  const t = useTranslations("children.identity");
  const [editing, setEditing] = useState(!child.idVerifiedAt);
  const [documentType, setDocumentType] = useState<IdDocumentType | "">(child.idDocumentType ?? "");
  const [note, setNote] = useState(child.idDocumentNote ?? "");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setEditing(!child.idVerifiedAt);
    setDocumentType(child.idDocumentType ?? "");
    setNote(child.idDocumentNote ?? "");
  }, [child.id, child.idVerifiedAt, child.idDocumentType, child.idDocumentNote]);

  const [nrnInput, setNrnInput] = useState("");
  const [nrnSaving, setNrnSaving] = useState(false);
  const [nrnError, setNrnError] = useState<string | null>(null);
  const [editingNrn, setEditingNrn] = useState(false);

  async function submitVerification() {
    if (!documentType) return;
    setSaving(true);
    setError(null);
    const success = await onVerify(documentType, note.trim() || null);
    setSaving(false);
    if (!success) {
      setError(t("saveError"));
      return;
    }
    setEditing(false);
  }

  async function submitNrn() {
    setNrnSaving(true);
    setNrnError(null);
    const success = await onSetNrn(nrnInput.trim());
    setNrnSaving(false);
    if (!success) {
      setNrnError(t("nrnInvalidFormat"));
      return;
    }
    setNrnInput("");
    setEditingNrn(false);
  }

  const showBothAttribution = !!child.idVerifiedAt && child.firstIdVerifiedAt !== child.idVerifiedAt;

  return (
    <div className="mt-8 border-t border-border pt-6 dark:border-border-dark">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-text dark:text-text-dark">{t("title")}</h2>
        {child.idVerifiedAt ? (
          <Badge variant="success" className="inline-flex items-center gap-1">
            <CheckCircle className="h-3 w-3" strokeWidth={2} />
            {t("verifiedBadge")}
          </Badge>
        ) : (
          <Badge variant="warning" className="inline-flex items-center gap-1">
            <Clock className="h-3 w-3" strokeWidth={2} />
            {t("unverifiedBadge")}
          </Badge>
        )}
      </div>

      {!editing && child.idVerifiedAt && (
        <div className="space-y-3">
          <dl className="divide-y divide-border dark:divide-border-dark">
            <div className="grid grid-cols-3 gap-4 py-3">
              <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("documentTypeLabel")}</dt>
              <dd className="col-span-2 text-sm text-text dark:text-text-dark">{t(`documentType.${child.idDocumentType}`)}</dd>
            </div>
            {child.idDocumentNote && (
              <div className="grid grid-cols-3 gap-4 py-3">
                <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("noteLabel")}</dt>
                <dd className="col-span-2 text-sm text-text dark:text-text-dark">{child.idDocumentNote}</dd>
              </div>
            )}
            <div className="grid grid-cols-3 gap-4 py-3">
              <dt className="text-sm text-text-soft dark:text-text-soft-dark">
                {showBothAttribution ? t("mostRecentlyVerifiedLabel") : t("verifiedByLabel")}
              </dt>
              <dd className="col-span-2 text-sm text-text dark:text-text-dark">
                {t("verifiedByValue", { email: child.idVerifiedByEmail ?? "", date: new Date(child.idVerifiedAt).toLocaleDateString() })}
              </dd>
            </div>
            {showBothAttribution && (
              <div className="grid grid-cols-3 gap-4 py-3">
                <dt className="text-sm text-text-soft dark:text-text-soft-dark">{t("firstVerifiedLabel")}</dt>
                <dd className="col-span-2 text-sm text-text dark:text-text-dark">
                  {t("verifiedByValue", { email: child.firstIdVerifiedByEmail ?? "", date: new Date(child.firstIdVerifiedAt!).toLocaleDateString() })}
                </dd>
              </div>
            )}
          </dl>
          <Button size="sm" variant="secondary" onClick={() => setEditing(true)}>{t("correctButton")}</Button>
        </div>
      )}

      {editing && (
        <div className="space-y-4">
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
          <div className="flex items-center gap-2">
            <Button size="sm" onClick={submitVerification} disabled={saving || !documentType}>
              {t("confirmButton")}
            </Button>
            {!!child.idVerifiedAt && (
              <Button size="sm" variant="secondary" onClick={() => setEditing(false)} disabled={saving}>
                {t("cancelButton")}
              </Button>
            )}
          </div>
        </div>
      )}

      <div className="mt-6 border-t border-border pt-6 dark:border-border-dark">
        <h3 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("nrnLabel")}</h3>
        {!editingNrn ? (
          <div className="flex items-center gap-3">
            <span className="text-sm text-text dark:text-text-dark">
              {child.nrnLast4 ? t("nrnMasked", { last4: child.nrnLast4 }) : t("notSet")}
            </span>
            <Button size="sm" variant="secondary" onClick={() => setEditingNrn(true)}>
              {child.nrnLast4 ? t("nrnReplaceButton") : t("nrnAddButton")}
            </Button>
          </div>
        ) : (
          <div className="max-w-xs space-y-2">
            <Input value={nrnInput} onChange={(e) => setNrnInput(e.target.value)} placeholder={t("nrnPlaceholder")} />
            {nrnError && <p className="text-sm text-danger dark:text-danger-dark">{nrnError}</p>}
            <div className="flex items-center gap-2">
              <Button size="sm" onClick={submitNrn} disabled={nrnSaving || !nrnInput.trim()}>{t("nrnSaveButton")}</Button>
              <Button size="sm" variant="secondary" onClick={() => { setEditingNrn(false); setNrnInput(""); setNrnError(null); }} disabled={nrnSaving}>
                {t("cancelButton")}
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
