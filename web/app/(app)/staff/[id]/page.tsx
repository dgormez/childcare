"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { ArrowLeft } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "../../../../components/ui/tabs";
import { StaffDossierTab } from "../../../../components/staff/StaffDossierTab";
import { StaffTimeEntriesTab } from "../../../../components/staff/StaffTimeEntriesTab";
import { ErrorState } from "../../../../components/ErrorState";
import type { StaffResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

/**
 * First staff detail screen (research.md R9, feature 028) — staff/page.tsx was list-only until
 * now. Dossier is the default/first tab (spec.md SC-002); Tijdsregistraties is the second.
 */
export default function StaffDetailPage() {
  const t = useTranslations("staff.detail");
  const router = useRouter();
  const params = useParams<{ id: string }>();

  const [staff, setStaff] = useState<StaffResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/staff/{id}", { params: { path: { id: params.id } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setStaff(result.data as unknown as StaffResponse);
    setState("loaded");
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  if (state === "loading") {
    return <div className="h-32 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />;
  }

  if (state === "error" || !staff) {
    return <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />;
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => router.push("/staff")}
        className="mb-4 inline-flex items-center gap-1 text-sm text-text-soft hover:text-text dark:text-text-soft-dark dark:hover:text-text-dark"
      >
        <ArrowLeft className="h-4 w-4" strokeWidth={2} />
        {t("back")}
      </button>

      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">
        {staff.firstName} {staff.lastName}
      </h1>

      <Tabs defaultValue="dossier">
        <TabsList>
          <TabsTrigger value="dossier">{t("tabDossier")}</TabsTrigger>
          <TabsTrigger value="timeEntries">{t("tabTimeEntries")}</TabsTrigger>
        </TabsList>

        <TabsContent value="dossier">
          <StaffDossierTab staffProfileId={staff.id} timeEntryFunctions={staff.timeEntryFunctions} />
        </TabsContent>

        <TabsContent value="timeEntries">
          <StaffTimeEntriesTab staffProfileId={staff.id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
