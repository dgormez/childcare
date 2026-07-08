"use client";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import type { StaffResponse, LocationResponse } from "../lib/types";

interface StaffTableProps {
  staff: StaffResponse[];
  locationsById: Map<string, string>;
  onResetPin: (staff: StaffResponse) => void;
  onToggleActive: (staff: StaffResponse) => void;
}

export function StaffTable({ staff, locationsById, onResetPin, onToggleActive }: StaffTableProps) {
  const t = useTranslations("staff");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnName")}</TableHead>
          <TableHead>{t("columnRole")}</TableHead>
          <TableHead>{t("columnLocations")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {staff.map((member) => {
          const active = !member.deactivatedAt;
          const locationNames = member.eligibleLocationIds
            .map((id) => locationsById.get(id))
            .filter((name): name is string => Boolean(name));

          return (
            <TableRow key={member.id}>
              <TableCell className="font-medium">
                {member.firstName} {member.lastName}
              </TableCell>
              <TableCell>{t(roleKey(member.role))}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">
                {locationNames.length > 0 ? locationNames.join(", ") : "—"}
              </TableCell>
              <TableCell>
                <Badge variant={active ? "success" : "neutral"}>
                  {active ? t("statusActive") : t("statusDeactivated")}
                </Badge>
              </TableCell>
              <TableCell className="text-right">
                <div className="flex justify-end gap-2">
                  <Button variant="ghost" size="sm" onClick={() => onResetPin(member)}>
                    {t("actionResetPin")}
                  </Button>
                  {/* Deactivate blocks login — reads as destructive (red), distinct from the
                      neutral actions, per design-system.md's destructive-as-text-button pattern.
                      Reactivate is the recovery path, so it stays neutral. */}
                  <Button
                    variant={active ? "destructive" : "ghost"}
                    size="sm"
                    onClick={() => onToggleActive(member)}
                  >
                    {active ? t("actionDeactivate") : t("actionReactivate")}
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

function roleKey(role: string): "role.director" | "role.staff" | "role.parent" {
  const normalized = role.toLowerCase();
  if (normalized === "director") return "role.director";
  if (normalized === "parent") return "role.parent";
  return "role.staff";
}

/** Builds the { id -> name } lookup StaffTable needs to resolve eligibleLocationIds to display
 * names — GET /api/staff never returns names, only ids (feature 005's data-model, unchanged by
 * this feature per data-model.md). */
export function toLocationsById(locations: LocationResponse[]): Map<string, string> {
  return new Map(locations.map((l) => [l.id, l.name]));
}
