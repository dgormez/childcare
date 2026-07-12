"use client";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Badge } from "./ui/badge";
import type { LocationResponse } from "../lib/types";

interface LocationsTableProps {
  locations: LocationResponse[];
}

/** platform-rules.md director-web: full-row click affordance rather than a small inline icon
 * button, since the row itself represents one record. */
export function LocationsTable({ locations }: LocationsTableProps) {
  const t = useTranslations("locations");
  const router = useRouter();

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnName")}</TableHead>
          <TableHead>{t("columnAddress")}</TableHead>
          <TableHead>{t("columnCapacity")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {locations.map((location) => {
          const deactivated = Boolean(location.deactivatedAt);
          return (
            <TableRow
              key={location.id}
              className="cursor-pointer"
              onClick={() => router.push(`/locations/${location.id}`)}
            >
              <TableCell className="font-medium">{location.name}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{location.address}</TableCell>
              <TableCell className="tabular-nums">{location.maxCapacity}</TableCell>
              <TableCell>
                <Badge variant={deactivated ? "danger" : "success"}>
                  {deactivated ? t("statusDeactivated") : t("statusActive")}
                </Badge>
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
