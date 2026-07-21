"use client";
import { useTranslations } from "next-intl";
import { ArrowUp, ArrowDown, Clock, Send, CheckCircle2, XCircle, Copy, Link2, CalendarRange, Globe, CalendarClock } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import type { WaitingListEntryResponse, WaitingListStatus } from "../lib/types";

interface WaitingListTableProps {
  entries: WaitingListEntryResponse[];
  onEdit: (entry: WaitingListEntryResponse) => void;
  onReorder: (entry: WaitingListEntryResponse, direction: "up" | "down") => void;
  onTransition: (entry: WaitingListEntryResponse, status: WaitingListStatus) => void;
  onLinkChild: (entry: WaitingListEntryResponse) => void;
  onViewOccupancy: (entry: WaitingListEntryResponse) => void;
  onTourInvitation: (entry: WaitingListEntryResponse) => void;
}

// Statuses aren't one of the platform's four locked semantics (danger/warning/success/info,
// design-system.md), so only `enrolled` — a genuine positive outcome — borrows `success`;
// the rest stay neutral. Every badge still pairs its color with a fixed icon per status.
const STATUS_ICON: Record<WaitingListStatus, typeof Clock> = {
  waiting: Clock,
  offered: Send,
  enrolled: CheckCircle2,
  withdrawn: XCircle,
};

function formatDate(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleDateString();
}

export function WaitingListTable({ entries, onEdit, onReorder, onTransition, onLinkChild, onViewOccupancy, onTourInvitation }: WaitingListTableProps) {
  const t = useTranslations("waitingList");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnContact")}</TableHead>
          <TableHead>{t("columnRequestedStart")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {entries.map((entry) => {
          const StatusIcon = STATUS_ICON[entry.status];
          return (
            <TableRow key={entry.id}>
              <TableCell className="font-medium">
                <button
                  onClick={() => onEdit(entry)}
                  className="text-left underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                >
                  {entry.childFirstName} {entry.childLastName}
                </button>
                {entry.source === "selfRegistered" && (
                  <Badge variant="neutral" className="ml-2">
                    <Globe className="mr-1 inline h-3 w-3" strokeWidth={2} />
                    {t("selfRegisteredBadge")}
                  </Badge>
                )}
                {entry.isDuplicate && (
                  <Badge variant="danger" className="ml-2">
                    <Copy className="mr-1 inline h-3 w-3" strokeWidth={2} />
                    {t("duplicateBadge")}
                  </Badge>
                )}
              </TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{entry.contactName}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
                {formatDate(entry.requestedStartDate)}
              </TableCell>
              <TableCell>
                <Badge variant={entry.status === "enrolled" ? "success" : "neutral"}>
                  <StatusIcon className="mr-1 inline h-3 w-3" strokeWidth={2} />
                  {t(`status.${entry.status}`)}
                </Badge>
              </TableCell>
              <TableCell className="text-right">
                <div className="flex items-center justify-end gap-1">
                  {entry.status === "waiting" && (
                    <>
                      <Button variant="ghost" size="sm" aria-label={t("moveUp")} onClick={() => onReorder(entry, "up")}>
                        <ArrowUp className="h-4 w-4" strokeWidth={2} />
                      </Button>
                      <Button variant="ghost" size="sm" aria-label={t("moveDown")} onClick={() => onReorder(entry, "down")}>
                        <ArrowDown className="h-4 w-4" strokeWidth={2} />
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => onTransition(entry, "offered")}>
                        {t("offer")}
                      </Button>
                      <Button variant="destructive" size="sm" onClick={() => onTransition(entry, "withdrawn")}>
                        {t("withdraw")}
                      </Button>
                    </>
                  )}
                  {entry.status === "offered" && (
                    <>
                      <Button variant="ghost" size="sm" onClick={() => onTransition(entry, "enrolled")}>
                        {t("enroll")}
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => onTransition(entry, "waiting")}>
                        {t("revertToWaiting")}
                      </Button>
                      <Button variant="destructive" size="sm" onClick={() => onTransition(entry, "withdrawn")}>
                        {t("withdraw")}
                      </Button>
                    </>
                  )}
                  {entry.status === "enrolled" && !entry.childId && (
                    <Button variant="ghost" size="sm" onClick={() => onLinkChild(entry)}>
                      <Link2 className="mr-1 h-4 w-4" strokeWidth={2} />
                      {t("linkChild")}
                    </Button>
                  )}
                  {(entry.status === "waiting" || entry.status === "offered") && (
                    <Button variant="ghost" size="sm" aria-label={t("tourInvitation.title")} onClick={() => onTourInvitation(entry)}>
                      <CalendarClock className="h-4 w-4" strokeWidth={2} />
                    </Button>
                  )}
                  <Button variant="ghost" size="sm" aria-label={t("viewOccupancy")} onClick={() => onViewOccupancy(entry)}>
                    <CalendarRange className="h-4 w-4" strokeWidth={2} />
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
