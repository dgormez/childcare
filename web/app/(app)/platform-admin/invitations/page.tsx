"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Mail, Plus } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { InvitationTable } from "../../../../components/platform-admin/InvitationTable";
import { InvitationFormDialog, type InvitationFormValues } from "../../../../components/platform-admin/InvitationFormDialog";
import { EmptyState } from "../../../../components/EmptyState";
import { ErrorState } from "../../../../components/ErrorState";
import { Button } from "../../../../components/ui/button";
import type { PlatformAdminInvitationResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function InvitationsPage() {
  const t = useTranslations("platformAdmin.invitations");

  const [invitations, setInvitations] = useState<PlatformAdminInvitationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/platform-admin/invitations");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setInvitations((result.data ?? []) as PlatformAdminInvitationResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function createInvitation(values: InvitationFormValues) {
    setSaving(true);
    const result = await (apiClient.POST as any)("/api/platform-admin/invitations", {
      body: { email: values.email.trim(), organisationNameNote: values.organisationNameNote.trim() || null, locale: values.locale },
    });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setDialogOpen(false);
    setNotice("");
    await load();
  }

  async function resend(invitation: PlatformAdminInvitationResponse) {
    const result = await (apiClient.POST as any)("/api/platform-admin/invitations/{id}/resend", {
      params: { path: { id: invitation.id } },
    });
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setNotice("");
    await load();
  }

  async function revoke(invitation: PlatformAdminInvitationResponse) {
    const result = await (apiClient.POST as any)("/api/platform-admin/invitations/{id}/revoke", {
      params: { path: { id: invitation.id } },
    });
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setNotice("");
    await load();
  }

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" strokeWidth={2} />
          {t("add")}
        </Button>
      </div>

      {notice && (
        <div className="mb-4 rounded-lg bg-surface-soft p-3 text-sm text-text dark:bg-surface-soft-dark dark:text-text-dark">
          {notice}
        </div>
      )}

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && invitations.length === 0 && <EmptyState icon={Mail} message={t("emptyState")} />}
      {state === "loaded" && invitations.length > 0 && (
        <InvitationTable invitations={invitations} onResend={resend} onRevoke={revoke} />
      )}

      <InvitationFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onSubmit={createInvitation}
        saving={saving}
      />
    </div>
  );
}
