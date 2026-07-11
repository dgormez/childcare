"use client";
import { useTranslations } from "next-intl";
import { CalendarOff, CalendarPlus, ArrowLeftRight, AlertTriangle } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import type { DayReservationResponse, DayReservationType } from "../lib/types";

interface DayReservationsTableProps {
  reservations: DayReservationResponse[];
  onApprove: (reservation: DayReservationResponse) => void;
  onReject: (reservation: DayReservationResponse) => void;
}

// Same fixed color+icon pairing rule as every other status badge in this app (design-system.md:
// never color alone) — one icon per type, reused everywhere the type appears.
const TYPE_ICON: Record<DayReservationType, typeof CalendarOff> = {
  absence: CalendarOff,
  extra: CalendarPlus,
  exchange: ArrowLeftRight,
};

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

export function DayReservationsTable({ reservations, onApprove, onReject }: DayReservationsTableProps) {
  const t = useTranslations("dayReservations");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnType")}</TableHead>
          <TableHead>{t("columnDate")}</TableHead>
          <TableHead>{t("columnReason")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {reservations.map((reservation) => {
          const TypeIcon = TYPE_ICON[reservation.type];
          return (
            <TableRow key={reservation.id}>
              <TableCell className="font-medium">{reservation.childDisplayName}</TableCell>
              <TableCell>
                <span className="inline-flex items-center gap-1.5">
                  <TypeIcon className="h-4 w-4 text-text-soft dark:text-text-soft-dark" strokeWidth={2} />
                  {t(`type.${reservation.type}`)}
                </span>
              </TableCell>
              <TableCell>
                <div>{formatDate(reservation.requestedDate)}</div>
                {reservation.type === "exchange" && reservation.exchangeForDate && (
                  <div className="text-xs text-text-soft dark:text-text-soft-dark">
                    {t("exchangeFor", { date: formatDate(reservation.exchangeForDate) })}
                  </div>
                )}
                {reservation.capacityWarning && (
                  <Badge variant="warning" className="mt-1 inline-flex items-center gap-1">
                    <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                    {t("capacityWarning")}
                  </Badge>
                )}
              </TableCell>
              <TableCell className="max-w-xs truncate text-text-soft dark:text-text-soft-dark">
                {reservation.reason ?? "—"}
              </TableCell>
              <TableCell className="text-right">
                <div className="inline-flex gap-2">
                  <Button variant="ghost" size="sm" onClick={() => onApprove(reservation)}>
                    {t("approve")}
                  </Button>
                  <Button variant="destructive" size="sm" onClick={() => onReject(reservation)}>
                    {t("reject")}
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
