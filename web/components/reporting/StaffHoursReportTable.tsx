"use client";
import { useTranslations } from "next-intl";
import type { StaffHoursReportResponse } from "../../lib/types";

/**
 * Medewerkersbeleid subsidy report table (spec.md FR-016, User Story 4) — ratios only, no
 * pass/fail evaluation against Opgroeien's thresholds (spec.md Clarifications; that belongs to
 * feature 041's future versioned ruleset).
 */
export function StaffHoursReportTable({ report }: { report: StaffHoursReportResponse }) {
  const t = useTranslations("staffHoursReport");

  return (
    <div>
      <p className="mb-4 text-sm text-text-soft dark:text-text-soft-dark">
        {t("totalChildHours")}: <span className="tabular-nums font-medium text-text dark:text-text-dark">{report.totalChildHours.toFixed(2)}</span>
      </p>
      <div className="overflow-x-auto rounded-xl border border-border dark:border-border-dark">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border text-left text-text-soft dark:border-border-dark dark:text-text-soft-dark">
              <th className="h-10 px-3 font-medium">{t("columns.function")}</th>
              <th className="h-10 px-3 font-medium">{t("columns.staffHours")}</th>
              <th className="h-10 px-3 font-medium">{t("columns.ratio")}</th>
            </tr>
          </thead>
          <tbody>
            {report.byFunction.map((row) => (
              <tr key={row.function} className="border-b border-border last:border-0 dark:border-border-dark">
                <td className="h-10 px-3 text-text dark:text-text-dark">{t(`functions.${row.function}`)}</td>
                <td className="h-10 px-3 tabular-nums text-text dark:text-text-dark">{row.totalStaffHours.toFixed(2)}</td>
                <td className="h-10 px-3 tabular-nums text-text dark:text-text-dark">
                  {row.ratio === null ? "—" : row.ratio.toFixed(2)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
