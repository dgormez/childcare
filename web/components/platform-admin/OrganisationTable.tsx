"use client";
import { useTranslations } from "next-intl";
import { CheckCircle2, Clock, AlertTriangle } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Badge, type BadgeProps } from "../ui/badge";
import type { PlatformAdminOrganisationResponse } from "../../lib/types";

interface OrganisationTableProps {
  organisations: PlatformAdminOrganisationResponse[];
}

// research.md R6: provisioningStatus is surfaced as-is, labeled honestly — this is NOT an
// admin-controlled active/suspended toggle, just a reflection of technical setup completion.
// "failed" reuses design-system.md's fixed danger→alert-triangle pairing (a genuine fault, not
// an admin action); "provisioning" reuses the fixed warning→clock pairing (in progress).
const STATUS_VARIANT: Record<string, BadgeProps["variant"]> = {
  ready: "success",
  provisioning: "warning",
  failed: "danger",
};

const STATUS_ICON: Record<string, typeof CheckCircle2> = {
  ready: CheckCircle2,
  provisioning: Clock,
  failed: AlertTriangle,
};

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

export function OrganisationTable({ organisations }: OrganisationTableProps) {
  const t = useTranslations("platformAdmin.organisations");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnName")}</TableHead>
          <TableHead>{t("columnPlan")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead>{t("columnKbo")}</TableHead>
          <TableHead>{t("columnCreated")}</TableHead>
          <TableHead>{t("columnRegisteredBy")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {organisations.map((org) => {
          const Icon = STATUS_ICON[org.provisioningStatus] ?? Clock;
          return (
            <TableRow key={org.id}>
              <TableCell className="font-medium">{org.name}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{org.plan}</TableCell>
              <TableCell>
                <Badge variant={STATUS_VARIANT[org.provisioningStatus] ?? "neutral"} className="inline-flex items-center gap-1">
                  <Icon className="h-3 w-3" strokeWidth={2} />
                  {t(`status.${org.provisioningStatus}`)}
                </Badge>
              </TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{org.kboNumber ?? "—"}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{formatDate(org.createdAt)}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{org.registeredByEmail ?? "—"}</TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
