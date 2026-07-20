"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Users, Plus, Clock } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { Badge } from "../../../components/ui/badge";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { ChildFormDialog, type ChildFormValues } from "../../../components/children/ChildFormDialog";
import type { ChildResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * Children list — 013c was the first to need a real /children screen; 006a adds the "New
 * child" create flow (FR-001/FR-014), the only way to create a child record through the UI.
 */
export default function ChildrenPage() {
  const t = useTranslations("children");
  const router = useRouter();
  const [children, setChildren] = useState<ChildResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createSaving, setCreateSaving] = useState(false);
  const [createSaveError, setCreateSaveError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/children");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setChildren(result.data as unknown as ChildResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function submitNewChild(values: ChildFormValues) {
    setCreateSaving(true);
    setCreateSaveError(null);
    const result = await apiClient.POST("/api/children", { body: values });
    setCreateSaving(false);
    if (!result.response.ok) {
      setCreateSaveError(t("form.saveError"));
      return;
    }
    setCreateDialogOpen(false);
    const created = result.data as unknown as ChildResponse;
    router.push(`/children/${created.id}`);
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button
          size="sm"
          className="inline-flex items-center gap-1"
          onClick={() => { setCreateSaveError(null); setCreateDialogOpen(true); }}
        >
          <Plus className="h-4 w-4" strokeWidth={2} />
          {t("newChild")}
        </Button>
      </div>

      {state === "loading" && <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && children.length === 0 && <EmptyState icon={Users} message={t("emptyState")} />}
      {state === "loaded" && children.length > 0 && (
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-border text-text-soft dark:border-border-dark dark:text-text-soft-dark">
              <th className="py-2 pr-4 font-medium">{t("columnName")}</th>
              <th className="py-2 pr-4 font-medium">{t("columnDateOfBirth")}</th>
              <th className="py-2 pr-4 font-medium">{t("columnStatus")}</th>
            </tr>
          </thead>
          <tbody>
            {children.map((child) => (
              <tr
                key={child.id}
                onClick={() => router.push(`/children/${child.id}`)}
                className="h-10 cursor-pointer border-b border-border last:border-0 hover:bg-surface-soft dark:border-border-dark dark:hover:bg-surface-soft-dark"
              >
                <td className="py-2 pr-4 text-text dark:text-text-dark">
                  <span className="inline-flex items-center gap-2">
                    {child.firstName} {child.lastName}
                    {!child.deactivatedAt && !child.idVerifiedAt && (
                      <Badge variant="warning" className="inline-flex items-center gap-1">
                        <Clock className="h-3 w-3" strokeWidth={2} />
                        {t("badgeUnverified")}
                      </Badge>
                    )}
                  </span>
                </td>
                <td className="py-2 pr-4 tabular-nums text-text-soft dark:text-text-soft-dark">{child.dateOfBirth}</td>
                <td className="py-2 pr-4 text-text-soft dark:text-text-soft-dark">
                  {child.deactivatedAt ? t("statusDeactivated") : t("statusActive")}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ChildFormDialog
        open={createDialogOpen}
        mode="create"
        child={null}
        onOpenChange={setCreateDialogOpen}
        onSubmit={submitNewChild}
        saving={createSaving}
        error={createSaveError}
      />
    </div>
  );
}
