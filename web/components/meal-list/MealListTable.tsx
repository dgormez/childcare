"use client";
import { Fragment } from "react";
import { useTranslations } from "next-intl";
import { Pill } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { AllergySeverityBadge } from "./AllergySeverityBadge";
import type { MealListGroupEntry, MealListChildEntry } from "../../lib/types";

interface MealListTableProps {
  groups: MealListGroupEntry[];
}

function ChildRow({ child }: { child: MealListChildEntry }) {
  const t = useTranslations("mealList");

  return (
    <TableRow>
      <TableCell className="font-medium">
        {child.firstName} {child.lastName}
      </TableCell>
      <TableCell>
        {child.hasPreference ? t(`texture.${child.texture}`) : (
          <span className="text-text-soft dark:text-text-soft-dark">{t("noPreference")}</span>
        )}
      </TableCell>
      <TableCell>
        {child.hasPreference && child.dietaryType.length > 0
          ? child.dietaryType.map((d) => t(`dietaryType.${d}`)).join(", ")
          : "—"}
      </TableCell>
      <TableCell>{child.hasPreference ? t(`portionSize.${child.portionSize}`) : "—"}</TableCell>
      <TableCell>
        <AllergySeverityBadge severity={child.allergySeverity} />
      </TableCell>
      <TableCell>
        {child.hasStandingMedication && (
          <Pill className="h-4 w-4 text-text-soft dark:text-text-soft-dark" strokeWidth={2} aria-label={t("standingMedication")} />
        )}
      </TableCell>
    </TableRow>
  );
}

export function MealListTable({ groups }: MealListTableProps) {
  const t = useTranslations("mealList");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnTexture")}</TableHead>
          <TableHead>{t("columnDietaryType")}</TableHead>
          <TableHead>{t("columnPortionSize")}</TableHead>
          <TableHead>{t("columnAllergySeverity")}</TableHead>
          <TableHead>{t("columnMedication")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {groups.map((group) => (
          <Fragment key={group.groupId}>
            <TableRow className="hover:bg-transparent">
              <TableCell colSpan={6} className="bg-surface-soft py-2 text-xs font-semibold uppercase tracking-wide text-text-soft dark:bg-surface-soft-dark dark:text-text-soft-dark">
                {group.groupName}
              </TableCell>
            </TableRow>
            {group.children.map((child) => (
              <ChildRow key={child.childId} child={child} />
            ))}
          </Fragment>
        ))}
      </TableBody>
    </Table>
  );
}
