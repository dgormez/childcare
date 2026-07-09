"use client";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import type { AttendanceRecordResponse } from "../lib/types";

interface AttendanceTableProps {
  records: AttendanceRecordResponse[];
  childNamesById: Map<string, string>;
  onCorrect: (record: AttendanceRecordResponse) => void;
}

function formatTime(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

export function AttendanceTable({ records, childNamesById, onCorrect }: AttendanceTableProps) {
  const t = useTranslations("attendance");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead>{t("columnCheckIn")}</TableHead>
          <TableHead>{t("columnCheckOut")}</TableHead>
          <TableHead>{t("columnPlannedDuration")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {records.map((record) => (
          <TableRow key={record.id}>
            <TableCell className="font-medium">{childNamesById.get(record.childId) ?? "—"}</TableCell>
            <TableCell>
              <Badge variant={record.status === "present" ? "success" : "neutral"}>
                {t(`status.${record.status}`)}
                {record.status === "absent" && record.absenceJustified !== null && (
                  <span className="ml-1">
                    ({record.absenceJustified ? t("justified") : t("unjustified")})
                  </span>
                )}
              </Badge>
            </TableCell>
            <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
              {formatTime(record.checkInAt)}
            </TableCell>
            <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
              {formatTime(record.checkOutAt)}
            </TableCell>
            <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
              {record.plannedDurationMinutes !== null ? t("minutes", { count: record.plannedDurationMinutes }) : "—"}
            </TableCell>
            <TableCell className="text-right">
              <Button variant="ghost" size="sm" onClick={() => onCorrect(record)}>
                {t("actionCorrect")}
              </Button>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
