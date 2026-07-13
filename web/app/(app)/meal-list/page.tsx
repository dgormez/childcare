"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { UtensilsCrossed, Printer } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { MealListTable } from "../../../components/meal-list/MealListTable";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { LocationResponse, MealListResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

function todayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

export default function MealListPage() {
  const t = useTranslations("mealList");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState<string>("");
  const [date, setDate] = useState(todayDateString());
  const [includeExpected, setIncludeExpected] = useState(false);
  const [mealList, setMealList] = useState<MealListResponse | null>(null);
  const [state, setState] = useState<LoadState>("loading");

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok) return;
      const fetched = result.data as unknown as LocationResponse[];
      setLocations(fetched);
      if (fetched.length > 0) setLocationId((current) => current || fetched[0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (!locationId) return;
    setState("loading");
    try {
      const result = await apiClient.GET("/api/locations/{locationId}/meal-list", {
        params: { path: { locationId }, query: { date, includeExpected } },
      });
      if (!result.response.ok) {
        setState("error");
        return;
      }
      setMealList(result.data as unknown as MealListResponse);
      setState("loaded");
    } catch {
      setState("error");
    }
  }, [locationId, date, includeExpected]);

  useEffect(() => {
    load();
  }, [load]);

  const totalChildren = mealList?.groups.reduce((sum, g) => sum + g.children.length, 0) ?? 0;

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4 print:hidden">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          {locations.length > 1 && (
            <select
              value={locationId}
              onChange={(e) => setLocationId(e.target.value)}
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
          <input
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            aria-label={t("dateLabel")}
            className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
          <label className="flex items-center gap-2 text-sm text-text dark:text-text-dark">
            <input
              type="checkbox"
              checked={includeExpected}
              onChange={(e) => setIncludeExpected(e.target.checked)}
              className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary dark:border-border-dark"
            />
            {t("includeExpected")}
          </label>
          <Button variant="secondary" onClick={() => window.print()}>
            <Printer className="h-4 w-4" strokeWidth={2} />
            {t("print")}
          </Button>
        </div>
      </div>

      <h1 className="mb-4 hidden text-xl font-semibold print:block">
        {t("title")} — {date}
      </h1>

      {state === "loading" && <div className="h-64 rounded-xl bg-surface-soft dark:bg-surface-soft-dark print:hidden" />}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && totalChildren === 0 && (
        <EmptyState icon={UtensilsCrossed} message={t("emptyState")} />
      )}

      {state === "loaded" && totalChildren > 0 && <MealListTable groups={mealList!.groups} />}

      {state === "loaded" && includeExpected && mealList?.expected && mealList.expected.children.length > 0 && (
        <div className="mt-6">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-text-soft dark:text-text-soft-dark">
            {t("expectedSectionTitle")}
          </h2>
          <MealListTable groups={[{ groupId: "expected", groupName: t("expectedSectionTitle"), children: mealList.expected.children }]} />
        </div>
      )}
    </div>
  );
}
