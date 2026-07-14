"use client";
import { useTranslations } from "next-intl";
import { ArrowUp, ArrowDown, CheckCircle2, XCircle } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Button } from "../ui/button";
import { Badge } from "../ui/badge";
import type { PlatformAdminVaccineTypeResponse, VaccineCategory } from "../../lib/types";

interface VaccineTypeManagementTableProps {
  entries: PlatformAdminVaccineTypeResponse[];
  onEdit: (entry: PlatformAdminVaccineTypeResponse) => void;
  onReorder: (entry: PlatformAdminVaccineTypeResponse, direction: "up" | "down") => void;
  onDeactivate: (entry: PlatformAdminVaccineTypeResponse) => void;
  onReactivate: (entry: PlatformAdminVaccineTypeResponse) => void;
}

function categoryLabel(t: ReturnType<typeof useTranslations>, category: VaccineCategory | null): string {
  return category ? t(`category.${category}`) : t("categoryNone");
}

function formatDateTime(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}

export function VaccineTypeManagementTable({ entries, onEdit, onReorder, onDeactivate, onReactivate }: VaccineTypeManagementTableProps) {
  const t = useTranslations("vaccineTypes");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnName")}</TableHead>
          <TableHead>{t("columnCategory")}</TableHead>
          <TableHead>{t("columnSortOrder")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {entries.map((entry) => (
          <TableRow key={entry.id}>
            <TableCell className="font-medium">
              <button
                onClick={() => onEdit(entry)}
                className="text-left underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              >
                {entry.name}
              </button>
            </TableCell>
            <TableCell className="text-text-soft dark:text-text-soft-dark">{categoryLabel(t, entry.category)}</TableCell>
            <TableCell className="text-text-soft dark:text-text-soft-dark" style={{ fontVariantNumeric: "tabular-nums" }}>
              {entry.sortOrder}
            </TableCell>
            <TableCell>
              <Badge variant={entry.isActive ? "success" : "neutral"}>
                {entry.isActive ? (
                  <CheckCircle2 className="mr-1 inline h-3 w-3" strokeWidth={2} />
                ) : (
                  <XCircle className="mr-1 inline h-3 w-3" strokeWidth={2} />
                )}
                {entry.isActive ? t("statusActive") : t("statusInactive")}
              </Badge>
              {!entry.isActive && (
                <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark">
                  {t("deactivatedBy", { email: entry.deactivatedByEmail ?? "", date: formatDateTime(entry.deactivatedAt) })}
                </p>
              )}
            </TableCell>
            <TableCell className="text-right">
              <div className="flex items-center justify-end gap-1">
                <Button variant="ghost" size="sm" aria-label={t("moveUp")} onClick={() => onReorder(entry, "up")}>
                  <ArrowUp className="h-4 w-4" strokeWidth={2} />
                </Button>
                <Button variant="ghost" size="sm" aria-label={t("moveDown")} onClick={() => onReorder(entry, "down")}>
                  <ArrowDown className="h-4 w-4" strokeWidth={2} />
                </Button>
                {entry.isActive ? (
                  <Button variant="destructive" size="sm" onClick={() => onDeactivate(entry)}>
                    {t("deactivate")}
                  </Button>
                ) : (
                  <Button variant="ghost" size="sm" onClick={() => onReactivate(entry)}>
                    {t("reactivate")}
                  </Button>
                )}
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
