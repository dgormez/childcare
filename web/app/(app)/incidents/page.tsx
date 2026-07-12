"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { ShieldAlert, ChevronLeft, ChevronRight } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { IncidentReportsTable } from "../../../components/IncidentReportsTable";
import { IncidentReportFilters } from "../../../components/IncidentReportFilters";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { IncidentReportResponse, LocationResponse, PagedIncidentReportsResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";
const PAGE_SIZE = 25;

interface ChildSummary {
  id: string;
  firstName: string;
  lastName: string;
}

export default function IncidentsPage() {
  const t = useTranslations("incidents");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [children, setChildren] = useState<ChildSummary[]>([]);
  const [reports, setReports] = useState<IncidentReportResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  const [childId, setChildId] = useState("");
  const [locationId, setLocationId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  // FR-009: a filter change re-narrows the result set, so a stale page number from a larger
  // prior set could point past the end of the new one — always restart at page 1.
  function updateFilter(setter: (value: string) => void) {
    return (value: string) => {
      setPage(1);
      setter(value);
    };
  }

  useEffect(() => {
    // Deactivated locations remain selectable in the filter (spec Edge Cases) — this feature's
    // own incident history reachability requirement is exactly why includeDeactivated is true
    // here, unlike attendance's active-only location list.
    apiClient.GET("/api/locations", { params: { query: { includeDeactivated: true } } }).then((result) => {
      if (result.response.ok) setLocations(result.data as unknown as LocationResponse[]);
    });
    apiClient.GET("/api/children").then((result) => {
      if (result.response.ok) setChildren(result.data as unknown as ChildSummary[]);
    });
  }, []);

  const load = useCallback(async () => {
    setState("loading");
    const result = await (apiClient.GET as any)("/api/incident-reports", {
      params: {
        query: {
          childId: childId || undefined,
          locationId: locationId || undefined,
          from: from ? new Date(from).toISOString() : undefined,
          to: to ? new Date(to).toISOString() : undefined,
          page,
          pageSize: PAGE_SIZE,
        },
      },
    });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    const data = result.data as unknown as PagedIncidentReportsResponse;
    setReports(data.items);
    setTotalCount(data.totalCount);
    setState("loaded");
  }, [childId, locationId, from, to, page]);

  useEffect(() => {
    load();
  }, [load]);

  const childNamesById = new Map(children.map((c) => [c.id, `${c.firstName} ${c.lastName}`]));
  const locationNamesById = new Map(locations.map((l) => [l.id, l.name]));

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      <IncidentReportFilters
        children={children}
        locations={locations}
        childId={childId}
        locationId={locationId}
        from={from}
        to={to}
        onChildIdChange={updateFilter(setChildId)}
        onLocationIdChange={updateFilter(setLocationId)}
        onFromChange={updateFilter(setFrom)}
        onToChange={updateFilter(setTo)}
      />

      {state === "loading" && <div className="h-64 rounded-lg bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && reports.length === 0 && <EmptyState icon={ShieldAlert} message={t("emptyState")} />}
      {state === "loaded" && reports.length > 0 && (
        <>
          <IncidentReportsTable reports={reports} childNamesById={childNamesById} locationNamesById={locationNamesById} />
          <div className="mt-4 flex items-center justify-between">
            <p className="text-sm text-text-soft dark:text-text-soft-dark">
              {t("pageOf", { page, totalPages: Math.max(1, Math.ceil(totalCount / PAGE_SIZE)) })}
            </p>
            <div className="flex gap-2">
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setPage((p) => p - 1)}
                disabled={page <= 1}
                className="inline-flex items-center gap-1"
              >
                <ChevronLeft className="h-4 w-4" strokeWidth={2} />
                {t("previousPage")}
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={page * PAGE_SIZE >= totalCount}
                className="inline-flex items-center gap-1"
              >
                {t("nextPage")}
                <ChevronRight className="h-4 w-4" strokeWidth={2} />
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
