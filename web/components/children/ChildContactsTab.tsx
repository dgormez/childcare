"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Users, Star, Trash2, CheckCircle, Clock, ShieldCheck } from "lucide-react";
import { apiClient } from "../../lib/apiClient";
import { Button } from "../ui/button";
import { Badge } from "../ui/badge";
import { EmptyState } from "../EmptyState";
import { ErrorState } from "../ErrorState";
import { ConfirmDialog } from "../ConfirmDialog";
import { LinkContactDialog } from "./LinkContactDialog";
import { ContactIdentityVerificationDialog } from "./ContactIdentityVerificationDialog";
import type { ChildContactResponse } from "../../lib/types";

interface ChildContactsTabProps {
  childId: string;
}

type LoadState = "loading" | "loaded" | "error";

const RELATIONSHIP_WIRE_TO_KEY: Record<string, string> = {
  Mother: "mother",
  Father: "father",
  Guardian: "guardian",
  EmergencyContact: "emergencyContact",
  AuthorisedPickup: "authorisedPickup",
  FosterParent: "fosterParent",
  Other: "other",
};

/** Feature 030 (US4) — the first web UI consumer of the already-existing (006/013)
 * contact-linking endpoints (spec.md FR-011/FR-012). Self-contained tab, mirroring
 * MilestonePortfolioView's own-fetch pattern on the child-detail screen. */
export function ChildContactsTab({ childId }: ChildContactsTabProps) {
  const t = useTranslations("children.contacts");
  const [contacts, setContacts] = useState<ChildContactResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<ChildContactResponse | null>(null);
  const [removing, setRemoving] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const [primaryError, setPrimaryError] = useState<string | null>(null);
  const [verifyTarget, setVerifyTarget] = useState<ChildContactResponse | null>(null);
  const tId = useTranslations("children.contacts.identity");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/children/{childId}/contacts", { params: { path: { childId } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setContacts(result.data as unknown as ChildContactResponse[]);
    setState("loaded");
  }, [childId]);

  useEffect(() => {
    load();
  }, [load]);

  async function confirmRemove() {
    if (!removeTarget) return;
    setRemoving(true);
    setRemoveError(null);
    const result = await apiClient.DELETE("/api/children/{childId}/contacts/{contactId}", {
      params: { path: { childId, contactId: removeTarget.contactId } },
    });
    setRemoving(false);
    if (!result.response.ok) {
      setRemoveError(t("removeError"));
      return;
    }
    setRemoveTarget(null);
    await load();
  }

  async function setPrimary(contact: ChildContactResponse) {
    setPrimaryError(null);
    const result = await apiClient.PUT("/api/children/{childId}/contacts/{contactId}", {
      params: { path: { childId, contactId: contact.contactId } },
      body: { relationship: contact.relationship, canPickup: contact.canPickup, isPrimary: true },
    });
    if (!result.response.ok) {
      setPrimaryError(t("setPrimaryError"));
      return;
    }
    await load();
  }

  if (state === "loading") return <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  if (state === "error") return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-text dark:text-text-dark">{t("relationshipLabel")}</h2>
        <Button size="sm" onClick={() => setDialogOpen(true)}>{t("addContact")}</Button>
      </div>

      {primaryError && <p className="mb-3 text-sm text-danger dark:text-danger-dark">{primaryError}</p>}

      {contacts.length === 0 ? (
        <EmptyState icon={Users} message={t("emptyState")} />
      ) : (
        <ul className="space-y-2">
          {contacts.map((contact) => (
            <li
              key={contact.contactId}
              className="flex items-center justify-between gap-3 rounded-lg bg-surface-soft px-4 py-3 dark:bg-surface-soft-dark"
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-text dark:text-text-dark">
                    {contact.firstName} {contact.lastName}
                  </span>
                  {contact.isPrimary && <Badge variant="neutral">{t("primaryBadge")}</Badge>}
                  {contact.idVerifiedAt ? (
                    <Badge variant="success" className="inline-flex items-center gap-1">
                      <CheckCircle className="h-3 w-3" strokeWidth={2} />
                      {tId("verifiedBadge")}
                    </Badge>
                  ) : (
                    <Badge variant="warning" className="inline-flex items-center gap-1">
                      <Clock className="h-3 w-3" strokeWidth={2} />
                      {tId("unverifiedBadge")}
                    </Badge>
                  )}
                </div>
                <p className="text-xs text-text-soft dark:text-text-soft-dark">
                  {t(`relationships.${RELATIONSHIP_WIRE_TO_KEY[contact.relationship] ?? "other"}`)}
                  {contact.phone && ` · ${contact.phone}`}
                </p>
              </div>
              <div className="flex items-center gap-1">
                <Button variant="ghost" size="sm" aria-label={tId("verifyAction")} onClick={() => setVerifyTarget(contact)}>
                  <ShieldCheck className="h-4 w-4" strokeWidth={2} />
                </Button>
                {!contact.isPrimary && (
                  <Button variant="ghost" size="sm" aria-label={t("setPrimary")} onClick={() => setPrimary(contact)}>
                    <Star className="h-4 w-4" strokeWidth={2} />
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={t("removeContact")}
                  onClick={() => {
                    setRemoveError(null);
                    setRemoveTarget(contact);
                  }}
                >
                  <Trash2 className="h-4 w-4" strokeWidth={2} />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <LinkContactDialog
        childId={childId}
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onLinked={load}
      />

      <ContactIdentityVerificationDialog
        contact={verifyTarget}
        open={!!verifyTarget}
        onOpenChange={(open) => !open && setVerifyTarget(null)}
        onVerified={load}
      />

      <ConfirmDialog
        open={!!removeTarget}
        onOpenChange={(open) => !open && setRemoveTarget(null)}
        title={t("removeConfirmTitle")}
        description={removeError ?? t("removeConfirmBody")}
        confirmLabel={t("removeConfirm")}
        cancelLabel={t("removeCancel")}
        onConfirm={confirmRemove}
        confirmDestructive
        confirming={removing}
      />
    </div>
  );
}
