"use client";
import { useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Info } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { apiClient } from "../../lib/apiClient";
import type { ContactResponse, ContactRelationship } from "../../lib/types";

interface LinkContactDialogProps {
  childId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onLinked: () => void;
  /** Feature 023 (FR-014): pre-fills the "create new contact" fields from a waiting-list
   * entry's contact details when opened from the enrollment-conversion flow, so the director
   * confirms rather than retypes. Optional — every other caller (feature 030) omits these and
   * gets the original empty-form behavior. */
  initialFirstName?: string;
  initialLastName?: string;
  initialPhone?: string;
  initialEmail?: string;
  initialRelationship?: ContactRelationship;
}

const RELATIONSHIPS: ContactRelationship[] = ["Mother", "Father", "Guardian", "EmergencyContact", "AuthorisedPickup", "FosterParent", "Other"];
const RELATIONSHIP_KEY: Record<ContactRelationship, string> = {
  Mother: "mother",
  Father: "father",
  Guardian: "guardian",
  EmergencyContact: "emergencyContact",
  AuthorisedPickup: "authorisedPickup",
  FosterParent: "fosterParent",
  Other: "other",
};

/** Feature 030 (US4) — "add contact" flow with client-side duplicate detection (spec.md
 * FR-013, research.md R7): fetches the tenant's full contact list once and filters for an
 * email/phone match as the director types, offering to link the existing contact instead of
 * creating a near-duplicate. */
export function LinkContactDialog({
  childId, open, onOpenChange, onLinked,
  initialFirstName, initialLastName, initialPhone, initialEmail, initialRelationship,
}: LinkContactDialogProps) {
  const t = useTranslations("children.contacts.dialog");
  const tc = useTranslations("children.contacts");
  const [allContacts, setAllContacts] = useState<ContactResponse[]>([]);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [locale, setLocale] = useState("nl");
  const [relationship, setRelationship] = useState<ContactRelationship>("Mother");
  const [canPickup, setCanPickup] = useState(true);
  const [useExisting, setUseExisting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setFirstName(initialFirstName ?? "");
    setLastName(initialLastName ?? "");
    setPhone(initialPhone ?? "");
    setEmail(initialEmail ?? "");
    setLocale("nl");
    setRelationship(initialRelationship ?? "Mother");
    setCanPickup(true);
    setUseExisting(false);
    setError(null);
    apiClient.GET("/api/contacts").then((result) => {
      if (result.response.ok) setAllContacts(result.data as unknown as ContactResponse[]);
    });
  }, [open, initialFirstName, initialLastName, initialPhone, initialEmail, initialRelationship]);

  const matchedContact = useMemo(() => {
    const normalizedEmail = email.trim().toLowerCase();
    const normalizedPhone = phone.trim();
    if (!normalizedEmail && !normalizedPhone) return null;
    return allContacts.find((c) =>
      (normalizedEmail && c.email?.toLowerCase() === normalizedEmail) ||
      (normalizedPhone && c.phone === normalizedPhone)) ?? null;
  }, [allContacts, email, phone]);

  async function submit() {
    setSaving(true);
    setError(null);

    let contactId = useExisting && matchedContact ? matchedContact.id : null;
    if (!contactId) {
      const createResult = await apiClient.POST("/api/contacts", {
        body: { firstName, lastName, phone, email: email || null, locale },
      });
      if (!createResult.response.ok) {
        setSaving(false);
        setError(t("saveError"));
        return;
      }
      contactId = (createResult.data as unknown as ContactResponse).id;
    }

    const linkResult = await apiClient.POST("/api/children/{childId}/contacts", {
      params: { path: { childId } },
      body: { contactId, relationship, canPickup, isPrimary: false },
    });
    setSaving(false);
    if (!linkResult.response.ok) {
      setError(t("saveError"));
      return;
    }
    onOpenChange(false);
    onLinked();
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          {useExisting && matchedContact ? (
            <div className="rounded-lg bg-surface-soft px-4 py-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
              {matchedContact.firstName} {matchedContact.lastName}
              <Button variant="ghost" size="sm" className="ml-2" onClick={() => setUseExisting(false)}>
                {t("createNew")}
              </Button>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-3">
                <label className="block text-sm font-medium text-text dark:text-text-dark">
                  {t("firstNameLabel")}
                  <Input className="mt-2" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
                </label>
                <label className="block text-sm font-medium text-text dark:text-text-dark">
                  {t("lastNameLabel")}
                  <Input className="mt-2" value={lastName} onChange={(e) => setLastName(e.target.value)} />
                </label>
              </div>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("phoneLabel")}
                <Input className="mt-2" value={phone} onChange={(e) => setPhone(e.target.value)} />
              </label>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("emailLabel")}
                <Input className="mt-2" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
              </label>
              <label className="block text-sm font-medium text-text dark:text-text-dark">
                {t("localeLabel")}
                <select
                  value={locale}
                  onChange={(e) => setLocale(e.target.value)}
                  className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
                >
                  <option value="nl">Nederlands</option>
                  <option value="fr">Français</option>
                  <option value="en">English</option>
                </select>
              </label>

              {matchedContact && (
                <div className="rounded-lg bg-warning px-4 py-3 text-sm text-warning-fg dark:bg-warning-dark">
                  <p className="flex items-center gap-2">
                    <Info className="h-4 w-4 shrink-0" strokeWidth={2} />
                    {t("matchFound")} {matchedContact.firstName} {matchedContact.lastName}
                  </p>
                  <Button variant="secondary" size="sm" className="mt-2" onClick={() => setUseExisting(true)}>
                    {t("linkExisting")}
                  </Button>
                </div>
              )}
            </>
          )}

          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm font-medium text-text dark:text-text-dark">
              {tc("relationshipLabel")}
              <select
                value={relationship}
                onChange={(e) => setRelationship(e.target.value as ContactRelationship)}
                className="mt-2 h-10 w-full rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
              >
                {RELATIONSHIPS.map((r) => (
                  <option key={r} value={r}>{tc(`relationships.${RELATIONSHIP_KEY[r]}`)}</option>
                ))}
              </select>
            </label>
            <label className="mt-8 flex items-center gap-2 text-sm font-medium text-text dark:text-text-dark">
              <input
                type="checkbox"
                checked={canPickup}
                onChange={(e) => setCanPickup(e.target.checked)}
                className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
              />
              {tc("canPickupLabel")}
            </label>
          </div>

          {error && <p className="text-sm text-danger dark:text-danger-dark">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button onClick={submit} disabled={saving || (!useExisting && (!firstName.trim() || !lastName.trim()))}>
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
