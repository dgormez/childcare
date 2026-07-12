"use client";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { AlertCircle, CheckCircle2 } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Badge } from "./ui/badge";
import type { IncidentReportResponse } from "../lib/types";

interface IncidentReportsTableProps {
  reports: IncidentReportResponse[];
  childNamesById: Map<string, string>;
  locationNamesById: Map<string, string>;
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

export function IncidentReportsTable({ reports, childNamesById, locationNamesById }: IncidentReportsTableProps) {
  const t = useTranslations("incidents");
  const router = useRouter();

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnLocation")}</TableHead>
          <TableHead>{t("columnOccurredAt")}</TableHead>
          <TableHead>{t("columnInjuryType")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {reports.map((report) => {
          const reviewed = report.reviewedAt !== null;
          return (
            <TableRow
              key={report.id}
              className="cursor-pointer"
              onClick={() => router.push(`/incidents/${report.id}`)}
            >
              <TableCell className="font-medium">{childNamesById.get(report.childId) ?? "—"}</TableCell>
              <TableCell>{locationNamesById.get(report.locationId) ?? "—"}</TableCell>
              <TableCell>{formatDateTime(report.occurredAt)}</TableCell>
              <TableCell>{t(`injuryTypes.${report.injuryType}`)}</TableCell>
              <TableCell>
                {/* FR-010/design-system.md: icon + color together, never color alone. */}
                {reviewed ? (
                  <Badge variant="neutral" className="inline-flex items-center gap-1.5">
                    <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
                    {t("reviewed")}
                  </Badge>
                ) : (
                  <Badge variant="warning" className="inline-flex items-center gap-1.5">
                    <AlertCircle className="h-3 w-3" strokeWidth={2} />
                    {t("unreviewed")}
                  </Badge>
                )}
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
