"use client";
import { useTranslations } from "next-intl";
import type { LocationResponse } from "../lib/types";

interface ChildSummary {
  id: string;
  firstName: string;
  lastName: string;
}

interface IncidentReportFiltersProps {
  children: ChildSummary[];
  // Deactivated locations MUST remain selectable — their historical incident reports remain
  // reachable per spec Edge Cases, so this list is never filtered to active-only.
  locations: LocationResponse[];
  childId: string;
  locationId: string;
  from: string;
  to: string;
  onChildIdChange: (value: string) => void;
  onLocationIdChange: (value: string) => void;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
}

export function IncidentReportFilters({
  children, locations, childId, locationId, from, to,
  onChildIdChange, onLocationIdChange, onFromChange, onToChange,
}: IncidentReportFiltersProps) {
  const t = useTranslations("incidents");

  return (
    <div className="mb-6 flex flex-wrap items-end gap-4">
      <div className="space-y-1">
        <label htmlFor="incident-child-filter" className="text-sm font-medium text-text dark:text-text-dark">
          {t("filterChild")}
        </label>
        <select
          id="incident-child-filter"
          value={childId}
          onChange={(e) => onChildIdChange(e.target.value)}
          className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
        >
          <option value="">{t("filterChildAll")}</option>
          {children.map((child) => (
            <option key={child.id} value={child.id}>{child.firstName} {child.lastName}</option>
          ))}
        </select>
      </div>

      <div className="space-y-1">
        <label htmlFor="incident-location-filter" className="text-sm font-medium text-text dark:text-text-dark">
          {t("filterLocation")}
        </label>
        <select
          id="incident-location-filter"
          value={locationId}
          onChange={(e) => onLocationIdChange(e.target.value)}
          className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
        >
          <option value="">{t("filterLocationAll")}</option>
          {locations.map((location) => (
            <option key={location.id} value={location.id}>{location.name}</option>
          ))}
        </select>
      </div>

      <div className="space-y-1">
        <label htmlFor="incident-from-filter" className="text-sm font-medium text-text dark:text-text-dark">
          {t("filterFrom")}
        </label>
        <input
          id="incident-from-filter"
          type="date"
          value={from}
          onChange={(e) => onFromChange(e.target.value)}
          className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
        />
      </div>

      <div className="space-y-1">
        <label htmlFor="incident-to-filter" className="text-sm font-medium text-text dark:text-text-dark">
          {t("filterTo")}
        </label>
        <input
          id="incident-to-filter"
          type="date"
          value={to}
          onChange={(e) => onToChange(e.target.value)}
          className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
        />
      </div>
    </div>
  );
}
