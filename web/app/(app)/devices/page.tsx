"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { Tablet } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { DevicesTable } from "../../../components/DevicesTable";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { DeviceSummaryResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function DevicesPage() {
  const t = useTranslations("devices");
  const [devices, setDevices] = useState<DeviceSummaryResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [revokeTarget, setRevokeTarget] = useState<DeviceSummaryResponse | null>(null);
  const [revoking, setRevoking] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    try {
      const result = await apiClient.GET("/api/devices");
      if (!result.response.ok) {
        setState("error");
        return;
      }
      setDevices(result.data as unknown as DeviceSummaryResponse[]);
      setState("loaded");
    } catch {
      setState("error");
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const handleRevoke = async () => {
    if (!revokeTarget) return;
    setRevoking(true);
    const result = await apiClient.POST("/api/devices/{id}/revoke", {
      params: { path: { id: revokeTarget.id } },
    });
    setRevoking(false);
    setRevokeTarget(null);
    if (result.response.ok) await load();
  };

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      {state === "loading" && (
        <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
      )}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && devices.length === 0 && <EmptyState icon={Tablet} message={t("emptyState")} />}

      {state === "loaded" && devices.length > 0 && (
        <DevicesTable devices={devices} onRevoke={setRevokeTarget} />
      )}

      {revokeTarget && (
        <ConfirmDialog
          open={Boolean(revokeTarget)}
          onOpenChange={(open) => !open && setRevokeTarget(null)}
          title={t("revokeDialogTitle")}
          description={t("revokeDialogDescription", { location: revokeTarget.locationName, group: revokeTarget.groupName })}
          confirmLabel={t("actionRevoke")}
          cancelLabel={t("revokeDialogCancel")}
          confirmDestructive
          confirming={revoking}
          onConfirm={handleRevoke}
        />
      )}
    </div>
  );
}
