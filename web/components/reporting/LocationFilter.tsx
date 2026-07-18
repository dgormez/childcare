"use client";
import { useTranslations } from "next-intl";
import type { LocationResponse } from "../../lib/types";

interface LocationFilterProps {
  locations: LocationResponse[];
  locationId: string;
  onLocationIdChange: (value: string) => void;
}

/** FR-013: narrows every dashboard section to one location; empty selection means the aggregate
 * across all the director's locations. */
export function LocationFilter({ locations, locationId, onLocationIdChange }: LocationFilterProps) {
  const t = useTranslations("dashboard.reporting.locationFilter");

  return (
    <div className="mb-6 space-y-1">
      <label htmlFor="reporting-location-filter" className="text-sm font-medium text-text dark:text-text-dark">
        {t("label")}
      </label>
      <select
        id="reporting-location-filter"
        value={locationId}
        onChange={(e) => onLocationIdChange(e.target.value)}
        className="h-10 rounded-lg bg-surface-soft px-3 text-sm text-text focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
      >
        <option value="">{t("allLocations")}</option>
        {locations.map((location) => (
          <option key={location.id} value={location.id}>{location.name}</option>
        ))}
      </select>
    </div>
  );
}
