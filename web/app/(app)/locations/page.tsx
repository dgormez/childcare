"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { MapPin, Plus } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { LocationsTable } from "../../../components/LocationsTable";
import { CreateLocationDialog, type CreateLocationValues } from "../../../components/CreateLocationDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { Button } from "../../../components/ui/button";
import type { LocationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function LocationsPage() {
  const t = useTranslations("locations");
  const router = useRouter();
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createSaving, setCreateSaving] = useState(false);
  const [createSaveError, setCreateSaveError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setState("loading");
    try {
      const result = await apiClient.GET("/api/locations");
      if (!result.response.ok) {
        setState("error");
        return;
      }
      setLocations(result.data as unknown as LocationResponse[]);
      setState("loaded");
    } catch {
      setState("error");
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function submitNewLocation(values: CreateLocationValues) {
    setCreateSaving(true);
    setCreateSaveError(null);
    const result = await apiClient.POST("/api/locations", { body: values });
    setCreateSaving(false);
    if (!result.response.ok) {
      setCreateSaveError(t("createError"));
      return;
    }
    setCreateDialogOpen(false);
    const created = result.data as unknown as LocationResponse;
    router.push(`/locations/${created.id}`);
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <Button onClick={() => { setCreateSaveError(null); setCreateDialogOpen(true); }}>
          <Plus className="h-4 w-4" strokeWidth={2} />
          {t("addButton")}
        </Button>
      </div>

      {state === "loading" && (
        <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
      )}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && locations.length === 0 && <EmptyState icon={MapPin} message={t("emptyState")} />}

      {state === "loaded" && locations.length > 0 && <LocationsTable locations={locations} />}

      <CreateLocationDialog
        open={createDialogOpen}
        onOpenChange={setCreateDialogOpen}
        onSubmit={submitNewLocation}
        saving={createSaving}
        error={createSaveError}
      />
    </div>
  );
}
