"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "../ui/button";
import { apiClient } from "../../lib/apiClient";
import type { StaffTimeEntryFunction } from "../../lib/types";

const FUNCTIONS: StaffTimeEntryFunction[] = ["kinderbegeleider", "logistiek", "verantwoordelijke"];

interface TimeEntryFunctionsFormProps {
  staffProfileId: string;
  initialFunctions: StaffTimeEntryFunction[];
}

/**
 * FR-010: which medewerkersbeleid function(s) this staff member may clock in under — at least
 * one required, or clock-in is rejected server-side (ClockInCommand's NoFunctionConfigured).
 */
export function TimeEntryFunctionsForm({ staffProfileId, initialFunctions }: TimeEntryFunctionsFormProps) {
  const t = useTranslations("staff.dossier");
  const tFn = useTranslations("staff.timeEntries");
  const [selected, setSelected] = useState<Set<StaffTimeEntryFunction>>(new Set(initialFunctions));
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function toggle(fn: StaffTimeEntryFunction) {
    setSaved(false);
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(fn)) next.delete(fn);
      else next.add(fn);
      return next;
    });
  }

  async function save() {
    if (selected.size === 0) {
      setError(t("noFunctionSelected"));
      return;
    }
    setSaving(true);
    setError(null);
    const result = await apiClient.PATCH("/api/staff/{id}/time-entry-functions", {
      params: { path: { id: staffProfileId } },
      body: { functions: Array.from(selected) },
    });
    setSaving(false);
    if (!result.response.ok) {
      setError(t("saveError"));
      return;
    }
    setSaved(true);
  }

  return (
    <div className="rounded-xl border border-border p-4 dark:border-border-dark">
      <h3 className="mb-3 text-sm font-semibold text-text dark:text-text-dark">{t("timeEntryFunctionsTitle")}</h3>
      {error && <p className="mb-2 text-sm text-danger dark:text-danger-dark">{error}</p>}
      <div className="mb-3 space-y-2">
        {FUNCTIONS.map((fn) => (
          <label key={fn} className="flex items-center gap-2 text-sm text-text dark:text-text-dark">
            <input type="checkbox" checked={selected.has(fn)} onChange={() => toggle(fn)} className="h-4 w-4" />
            {tFn(`functions.${fn}`)}
          </label>
        ))}
      </div>
      <Button size="sm" onClick={save} disabled={saving}>
        {saved ? t("saved") : t("save")}
      </Button>
    </div>
  );
}
