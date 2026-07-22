"use client";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import type { StaffLeaveRequestResponse, StaffLeaveRequestStatus } from "../lib/types";

interface LeaveRequestTableProps {
  requests: StaffLeaveRequestResponse[];
  staffNamesById: Map<string, string>;
  onApprove: (request: StaffLeaveRequestResponse) => void;
  onReject: (request: StaffLeaveRequestResponse) => void;
}

const STATUS_VARIANT: Record<StaffLeaveRequestStatus, "neutral" | "success" | "danger"> = {
  pending: "neutral",
  approved: "success",
  rejected: "danger",
};

function formatDate(value: string): string {
  return new Date(`${value}T00:00:00`).toLocaleDateString();
}

/** FR-010: the director's "Verlofaanvragen" queue — every cell action is a real <button>,
 * matching SchedulingGrid.tsx's own accessibility rule (spec.md UX Requirements). */
export function LeaveRequestTable({ requests, staffNamesById, onApprove, onReject }: LeaveRequestTableProps) {
  const t = useTranslations("leaveRequests");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnStaff")}</TableHead>
          <TableHead>{t("columnType")}</TableHead>
          <TableHead>{t("columnDateRange")}</TableHead>
          <TableHead>{t("columnNotes")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {requests.map((request) => (
          <TableRow key={request.id}>
            <TableCell className="font-medium">{staffNamesById.get(request.staffProfileId) ?? t("unknownStaff")}</TableCell>
            <TableCell>{t(`type.${request.type}`)}</TableCell>
            <TableCell>
              {request.dateFrom === request.dateTo
                ? formatDate(request.dateFrom)
                : `${formatDate(request.dateFrom)} – ${formatDate(request.dateTo)}`}
            </TableCell>
            <TableCell className="max-w-xs truncate text-text-soft dark:text-text-soft-dark">{request.notes ?? "—"}</TableCell>
            <TableCell>
              <Badge variant={STATUS_VARIANT[request.status]}>{t(`status.${request.status}`)}</Badge>
            </TableCell>
            <TableCell className="text-right">
              {request.status === "pending" ? (
                <div className="flex justify-end gap-2">
                  <Button size="sm" onClick={() => onApprove(request)}>
                    {t("approve")}
                  </Button>
                  <Button variant="destructive" size="sm" onClick={() => onReject(request)}>
                    {t("reject")}
                  </Button>
                </div>
              ) : (
                <span className="text-xs text-text-soft dark:text-text-soft-dark">—</span>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
