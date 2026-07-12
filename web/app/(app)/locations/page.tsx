"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { MapPin } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { LocationsTable } from "../../../components/LocationsTable";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { LocationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function LocationsPage() {
  const t = useTranslations("locations");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");

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

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      {state === "loading" && (
        <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
      )}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && locations.length === 0 && <EmptyState icon={MapPin} message={t("emptyState")} />}

      {state === "loaded" && locations.length > 0 && <LocationsTable locations={locations} />}
    </div>
  );
}
