"use client";
import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "../../../lib/apiClient";
import { ContractExpiryBlock } from "../../../components/staff/ContractExpiryBlock";
import { LocationFilter } from "../../../components/reporting/LocationFilter";
import { OccupancySection } from "../../../components/reporting/OccupancySection";
import { BkrComplianceSection } from "../../../components/reporting/BkrComplianceSection";
import { AttendanceSummarySection } from "../../../components/reporting/AttendanceSummarySection";
import { InvoiceStatusSection } from "../../../components/reporting/InvoiceStatusSection";
import { DataCompletenessSection } from "../../../components/reporting/DataCompletenessSection";
import type { LocationResponse } from "../../../lib/types";

// First screen in this app with dashboard-shaped content (feature 013c's due-soon block).
// Feature 018 extends this same page with the management-reporting sections, per this
// comment's own original intent — a future feature adding more widgets extends this page.
export default function DashboardPage() {
  const t = useTranslations("dashboard");
  const [locations, setLocations] = useState<LocationResponse[]>([]);
  const [locationId, setLocationId] = useState("");

  useEffect(() => {
    apiClient.GET("/api/locations").then((result) => {
      if (!result.response.ok) return;
      setLocations(result.data as unknown as LocationResponse[]);
    });
  }, []);

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      {locations.length > 1 && (
        <LocationFilter locations={locations} locationId={locationId} onLocationIdChange={setLocationId} />
      )}

      <div className="space-y-8">
        <OccupancySection locationId={locationId} />
        <BkrComplianceSection locationId={locationId} />
        <InvoiceStatusSection locationId={locationId} />
        <DataCompletenessSection locationId={locationId} />
        <ContractExpiryBlock />
        <AttendanceSummarySection locationId={locationId} />
      </div>
    </div>
  );
}
