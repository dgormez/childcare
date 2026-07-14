"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Syringe, Plus } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { useAuth } from "../../../../components/AuthProvider";
import { VaccineTypeManagementTable } from "../../../../components/platform-admin/VaccineTypeManagementTable";
import { VaccineTypeFormDialog, type VaccineTypeFormValues } from "../../../../components/platform-admin/VaccineTypeFormDialog";
import { EmptyState } from "../../../../components/EmptyState";
import { ErrorState } from "../../../../components/ErrorState";
import { Button } from "../../../../components/ui/button";
import type { PlatformAdminVaccineTypeResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

// FR-003: reachable only when the authenticated director's token carries the platform-admin
// flag — session.user.isPlatformAdmin is resolved server-side (AuthenticatedUser.IsPlatformAdmin)
// since this app never decodes the JWT client-side. Redirects rather than 404s, matching how
// AppLayout already redirects an unauthenticated visitor to /login.
export default function VaccineTypeManagementPage() {
  const t = useTranslations("vaccineTypes");
  const { session } = useAuth();
  const router = useRouter();

  const [entries, setEntries] = useState<PlatformAdminVaccineTypeResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<PlatformAdminVaccineTypeResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (session && !session.user.isPlatformAdmin) router.replace("/dashboard");
  }, [session, router]);

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/platform-admin/vaccine-types");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setEntries((result.data ?? []) as PlatformAdminVaccineTypeResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function saveEntry(values: VaccineTypeFormValues) {
    setSaving(true);
    const body = { name: values.name.trim(), category: values.category || null };
    const result = editing
      ? await (apiClient.PATCH as any)("/api/platform-admin/vaccine-types/{id}", { params: { path: { id: editing.id } }, body })
      : await (apiClient.POST as any)("/api/platform-admin/vaccine-types", { body });
    setSaving(false);
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setDialogOpen(false);
    setEditing(null);
    setNotice("");
    await load();
  }

  async function reorder(entry: PlatformAdminVaccineTypeResponse, direction: "up" | "down") {
    const result = await (apiClient.POST as any)("/api/platform-admin/vaccine-types/{id}/reorder", {
      params: { path: { id: entry.id } },
      body: { direction },
    });
    if (!result.response.ok) {
      setNotice(t("reorderBoundaryError"));
      return;
    }
    setEntries((result.data ?? []) as PlatformAdminVaccineTypeResponse[]);
    setNotice("");
  }

  async function deactivate(entry: PlatformAdminVaccineTypeResponse) {
    const result = await (apiClient.POST as any)("/api/platform-admin/vaccine-types/{id}/deactivate", {
      params: { path: { id: entry.id } },
    });
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setNotice("");
    await load();
  }

  async function reactivate(entry: PlatformAdminVaccineTypeResponse) {
    const result = await (apiClient.POST as any)("/api/platform-admin/vaccine-types/{id}/reactivate", {
      params: { path: { id: entry.id } },
    });
    if (!result.response.ok) {
      setNotice(t("genericError"));
      return;
    }
    setNotice("");
    await load();
  }

  if (session && !session.user.isPlatformAdmin) return null;

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button onClick={() => { setEditing(null); setDialogOpen(true); }}>
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
      {state === "loaded" && entries.length === 0 && <EmptyState icon={Syringe} message={t("emptyState")} />}
      {state === "loaded" && entries.length > 0 && (
        <VaccineTypeManagementTable
          entries={entries}
          onEdit={(entry) => { setEditing(entry); setDialogOpen(true); }}
          onReorder={reorder}
          onDeactivate={deactivate}
          onReactivate={reactivate}
        />
      )}

      <VaccineTypeFormDialog
        open={dialogOpen}
        entry={editing}
        onOpenChange={(open) => { setDialogOpen(open); if (!open) setEditing(null); }}
        onSubmit={saveEntry}
        saving={saving}
      />
    </div>
  );
}
