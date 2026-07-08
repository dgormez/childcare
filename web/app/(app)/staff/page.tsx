"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Users, Search } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { StaffTable, toLocationsById } from "../../../components/StaffTable";
import { PinResetDialog } from "../../../components/PinResetDialog";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import { Input } from "../../../components/ui/input";
import type { StaffResponse, LocationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function StaffPage() {
  const t = useTranslations("staff");
  const [staff, setStaff] = useState<StaffResponse[]>([]);
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [search, setSearch] = useState("");
  const [pinTarget, setPinTarget] = useState<StaffResponse | null>(null);
  const [toggleTarget, setToggleTarget] = useState<StaffResponse | null>(null);
  const [toggling, setToggling] = useState(false);

  const load = useCallback(async () => {
    setState("loading");
    try {
      const [staffResult, locationsResult] = await Promise.all([
        apiClient.GET("/api/staff", { params: { query: { includeDeactivated: true } } }),
        apiClient.GET("/api/locations"),
      ]);
      if (!staffResult.response.ok || !locationsResult.response.ok) {
        setState("error");
        return;
      }
      setStaff(staffResult.data as unknown as StaffResponse[]);
      setLocations(locationsResult.data as unknown as LocationResponse[]);
      setState("loaded");
    } catch {
      setState("error");
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const locationsById = useMemo(() => toLocationsById(locations), [locations]);

  const filteredStaff = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return staff;
    return staff.filter((member) => `${member.firstName} ${member.lastName}`.toLowerCase().includes(query));
  }, [staff, search]);

  const handlePinSubmit = async (pin: string): Promise<{ ok: true } | { ok: false; errorKey: string }> => {
    if (!pinTarget) return { ok: false, errorKey: "errors.unexpected" };
    const result = await apiClient.PUT("/api/staff/{id}/pin", {
      params: { path: { id: pinTarget.id } },
      body: { pin },
    });
    if (result.response.ok) return { ok: true };
    const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey ?? "errors.unexpected";
    return { ok: false, errorKey };
  };

  const handleToggleActive = async () => {
    if (!toggleTarget) return;
    setToggling(true);
    const active = !toggleTarget.deactivatedAt;
    const path = active ? "/api/staff/{id}/deactivate" : "/api/staff/{id}/reactivate";
    const result = await apiClient.POST(path, { params: { path: { id: toggleTarget.id } } });
    setToggling(false);
    setToggleTarget(null);
    if (result.response.ok) await load();
  };

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="relative w-64">
          <Search className="pointer-events-none absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("searchPlaceholder")}
            className="pl-8"
            aria-label={t("searchPlaceholder")}
          />
        </div>
      </div>

      {state === "loading" && (
        <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
      )}

      {state === "error" && (
        <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />
      )}

      {state === "loaded" && staff.length === 0 && (
        <EmptyState icon={Users} message={t("emptyState")} />
      )}

      {state === "loaded" && staff.length > 0 && (
        <StaffTable
          staff={filteredStaff}
          locationsById={locationsById}
          onResetPin={setPinTarget}
          onToggleActive={setToggleTarget}
        />
      )}

      {pinTarget && (
        <PinResetDialog
          open={Boolean(pinTarget)}
          onOpenChange={(open) => !open && setPinTarget(null)}
          staffName={`${pinTarget.firstName} ${pinTarget.lastName}`}
          onSubmit={handlePinSubmit}
        />
      )}

      {toggleTarget && (
        <ConfirmDialog
          open={Boolean(toggleTarget)}
          onOpenChange={(open) => !open && setToggleTarget(null)}
          title={toggleTarget.deactivatedAt ? t("reactivateDialogTitle") : t("deactivateDialogTitle")}
          description={t("toggleDialogDescription", { name: `${toggleTarget.firstName} ${toggleTarget.lastName}` })}
          confirmLabel={toggleTarget.deactivatedAt ? t("actionReactivate") : t("actionDeactivate")}
          cancelLabel={t("pinDialogCancel")}
          confirmDestructive={!toggleTarget.deactivatedAt}
          confirming={toggling}
          onConfirm={handleToggleActive}
        />
      )}
    </div>
  );
}
