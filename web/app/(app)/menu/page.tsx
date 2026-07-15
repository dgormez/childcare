"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "../../../lib/apiClient";
import { ErrorState } from "../../../components/ErrorState";
import { MonthlyMenuDayGrid, type MonthlyMenuDaySave } from "../../../components/menu/MonthlyMenuDayGrid";
import { MonthlyMenuVariantSelector } from "../../../components/menu/MonthlyMenuVariantSelector";
import { MealPreferenceRequestQueue } from "../../../components/menu/MealPreferenceRequestQueue";
import type { LocationResponse, MonthlyMenuResponse, MonthlyMenuPublishStateResponse, MealPreferenceChangeRequestResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

function currentYearMonth(): { year: number; month: number } {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

export default function MonthlyMenuPage() {
  const t = useTranslations("menu");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState<string>("");
  const [{ year, month }, setYearMonth] = useState(currentYearMonth());
  const [variant, setVariant] = useState<string | null>(null);
  const [menu, setMenu] = useState<MonthlyMenuResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [saving, setSaving] = useState(false);
  const [pendingRequests, setPendingRequests] = useState<MealPreferenceChangeRequestResponse[]>([]);

  const selectedLocation = locations.find((loc) => loc.id === locationId);
  const availableVariants = selectedLocation?.menuVariantPriorityOrder ?? [];

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok) return;
      const fetched = result.data as unknown as LocationResponse[];
      setLocations(fetched);
      if (fetched.length > 0) setLocationId((current) => current || fetched[0].id);
    });
  }, []);

  const loadPendingRequests = useCallback(async () => {
    const result = await apiClient.GET("/api/meal-preference-requests", { params: { query: { status: "pending" } } });
    if (result.response.ok) setPendingRequests(result.data as unknown as MealPreferenceChangeRequestResponse[]);
  }, []);

  useEffect(() => {
    loadPendingRequests();
  }, [loadPendingRequests]);

  const handleApproveRequest = async (request: MealPreferenceChangeRequestResponse) => {
    const result = await apiClient.POST("/api/meal-preference-requests/{id}/approve", { params: { path: { id: request.id } } });
    if (result.response.ok) await loadPendingRequests();
  };

  const handleRejectRequest = async (request: MealPreferenceChangeRequestResponse, reason: string | null) => {
    const result = await apiClient.POST("/api/meal-preference-requests/{id}/reject", {
      params: { path: { id: request.id } },
      body: { reason },
    });
    if (result.response.ok) await loadPendingRequests();
  };

  const load = useCallback(async () => {
    if (!locationId) return;
    setState("loading");
    try {
      const result = await apiClient.GET("/api/locations/{locationId}/monthly-menus/{year}/{month}", {
        params: { path: { locationId, year, month }, query: { variant: variant ?? undefined } },
      });
      if (!result.response.ok) {
        setState("error");
        return;
      }
      setMenu(result.data as unknown as MonthlyMenuResponse);
      setState("loaded");
    } catch {
      setState("error");
    }
  }, [locationId, year, month, variant]);

  useEffect(() => {
    load();
  }, [load]);

  const handleSave = async (days: MonthlyMenuDaySave[]) => {
    setSaving(true);
    try {
      const result = await apiClient.PUT("/api/locations/{locationId}/monthly-menus/{year}/{month}", {
        params: { path: { locationId, year, month }, query: { variant: variant ?? undefined } },
        body: { days },
      });
      if (result.response.ok) setMenu(result.data as unknown as MonthlyMenuResponse);
    } finally {
      setSaving(false);
    }
  };

  const handlePublish = async () => {
    setSaving(true);
    try {
      const result = await apiClient.POST("/api/locations/{locationId}/monthly-menus/{year}/{month}/publish", {
        params: { path: { locationId, year, month }, query: { variant: variant ?? undefined } },
      });
      if (result.response.ok) {
        const publishState = result.data as unknown as MonthlyMenuPublishStateResponse;
        setMenu((current) => (current ? { ...current, isPublished: publishState.isPublished, publishedAt: publishState.publishedAt } : current));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleUnpublish = async () => {
    setSaving(true);
    try {
      const result = await apiClient.POST("/api/locations/{locationId}/monthly-menus/{year}/{month}/unpublish", {
        params: { path: { locationId, year, month }, query: { variant: variant ?? undefined } },
      });
      if (result.response.ok) {
        const publishState = result.data as unknown as MonthlyMenuPublishStateResponse;
        setMenu((current) => (current ? { ...current, isPublished: publishState.isPublished, publishedAt: publishState.publishedAt } : current));
      }
    } finally {
      setSaving(false);
    }
  };

  const monthInputValue = `${year}-${String(month).padStart(2, "0")}`;

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          {locations.length > 1 && (
            <select
              value={locationId}
              onChange={(e) => {
                setLocationId(e.target.value);
                setVariant(null);
              }}
              aria-label={t("locationLabel")}
              className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
            >
              {locations.map((loc) => (
                <option key={loc.id} value={loc.id}>
                  {loc.name}
                </option>
              ))}
            </select>
          )}
          <MonthlyMenuVariantSelector availableVariants={availableVariants} value={variant} onChange={setVariant} />
          <input
            type="month"
            value={monthInputValue}
            onChange={(e) => {
              const [y, m] = e.target.value.split("-").map(Number);
              if (y && m) setYearMonth({ year: y, month: m });
            }}
            aria-label={t("monthLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
        </div>
      </div>

      {state === "loading" && <div className="h-64 rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && menu && (
        <MonthlyMenuDayGrid year={year} month={month} menu={menu} saving={saving} onSave={handleSave} onPublish={handlePublish} onUnpublish={handleUnpublish} />
      )}

      {pendingRequests.length > 0 && (
        <div className="mt-8">
          <MealPreferenceRequestQueue requests={pendingRequests} onApprove={handleApproveRequest} onReject={handleRejectRequest} />
        </div>
      )}
    </div>
  );
}
