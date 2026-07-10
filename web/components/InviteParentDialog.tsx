"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { apiClient } from "../lib/apiClient";
import type { ApiErrorBody, ContactResponse } from "../lib/types";

interface InviteParentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

// No per-child Contacts management screen exists in web/ yet (children/page.tsx is still a
// NotYetAvailable placeholder) — this dialog searches the tenant-wide contact list directly
// (GET /api/contacts, feature 006) rather than a per-child picker. CanPickup eligibility is
// enforced server-side (POST /api/parent-invitations); only the "no email on file" case is
// checked client-side, since ContactResponse carries Email but not a cross-child CanPickup flag.
export function InviteParentDialog({ open, onOpenChange }: InviteParentDialogProps) {
  const t = useTranslations("messages");
  const [contacts, setContacts] = useState<ContactResponse[]>([]);
  const [search, setSearch] = useState("");
  const [sendingId, setSendingId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<{ contactId: string; message: string } | null>(null);
  const [sentIds, setSentIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!open) return;
    setSearch("");
    setFeedback(null);
    (apiClient.GET as any)("/api/contacts").then((result: { response: Response; data?: ContactResponse[] }) => {
      if (result.response.ok && result.data) setContacts(result.data);
    });
  }, [open]);

  const filtered = contacts.filter((c) => {
    const q = search.trim().toLowerCase();
    if (!q) return true;
    return `${c.firstName} ${c.lastName} ${c.email ?? ""}`.toLowerCase().includes(q);
  });

  function errorKeyToMessage(errorKey: string): string {
    switch (errorKey) {
      case "errors.parent_invitation.already_has_account":
        return t("inviteErrorAlreadyHasAccount");
      case "errors.validation":
        return t("inviteErrorNotEligible");
      default:
        return t("inviteErrorGeneric");
    }
  }

  async function sendInvite(contact: ContactResponse) {
    setSendingId(contact.id);
    setFeedback(null);
    const result = await (apiClient.POST as any)("/api/parent-invitations", { body: { contactId: contact.id } });
    setSendingId(null);
    if (!result.response.ok) {
      const errorKey = ((result.error ?? {}) as ApiErrorBody).errorKey ?? "";
      setFeedback({ contactId: contact.id, message: errorKeyToMessage(errorKey) });
      return;
    }
    setSentIds((prev) => new Set(prev).add(contact.id));
    setFeedback({ contactId: contact.id, message: t("inviteSent") });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("inviteDialogTitle")}</DialogTitle>
          <DialogDescription>{t("inviteDialogDescription")}</DialogDescription>
        </DialogHeader>

        <Input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t("inviteSearchPlaceholder")}
          aria-label={t("inviteSearchPlaceholder")}
        />

        <div className="max-h-80 space-y-1 overflow-y-auto">
          {filtered.map((contact) => {
            const noEmail = !contact.email;
            const alreadySent = sentIds.has(contact.id);
            return (
              <div
                key={contact.id}
                className="flex items-center justify-between gap-3 rounded-lg px-2 py-2 hover:bg-surface-soft dark:hover:bg-surface-soft-dark"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-text dark:text-text-dark">
                    {contact.firstName} {contact.lastName}
                  </p>
                  <p className="truncate text-xs text-text-soft dark:text-text-soft-dark">
                    {contact.email ?? t("inviteNoEmail")}
                  </p>
                  {feedback?.contactId === contact.id && (
                    <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark">{feedback.message}</p>
                  )}
                </div>
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={noEmail || sendingId === contact.id || alreadySent}
                  onClick={() => sendInvite(contact)}
                >
                  {t("inviteSend")}
                </Button>
              </div>
            );
          })}
        </div>
      </DialogContent>
    </Dialog>
  );
}
