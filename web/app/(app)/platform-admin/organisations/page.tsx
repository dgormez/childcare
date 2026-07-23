"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Building2 } from "lucide-react";
import { apiClient } from "../../../../lib/apiClient";
import { OrganisationTable } from "../../../../components/platform-admin/OrganisationTable";
import { EmptyState } from "../../../../components/EmptyState";
import { ErrorState } from "../../../../components/ErrorState";
import type { PlatformAdminOrganisationResponse } from "../../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

// FR-012/FR-013: read-only directory — no create/edit/delete affordance anywhere on this page.
export default function OrganisationsPage() {
  const t = useTranslations("platformAdmin.organisations");

  const [organisations, setOrganisations] = useState<PlatformAdminOrganisationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/platform-admin/organisations");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setOrganisations((result.data ?? []) as PlatformAdminOrganisationResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
      </div>

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && organisations.length === 0 && <EmptyState icon={Building2} message={t("emptyState")} />}
      {state === "loaded" && organisations.length > 0 && <OrganisationTable organisations={organisations} />}
    </div>
  );
}
