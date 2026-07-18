import { useTranslations } from "next-intl";
import { Badge } from "../ui/badge";
import { STATUS_ICON, STATUS_BADGE_VARIANT } from "./statusIcon";
import type { OccupancyStatus } from "../../lib/types";

/** FR-018/FR-020: colour paired with an icon, never colour alone. Renders nothing (a plain
 * headcount is shown by the caller instead) when status is null — a group with no capacity set
 * has no meaningful colour to convey (Edge Cases). */
export function StatusBadge({ status }: { status: OccupancyStatus | null }) {
  const t = useTranslations("dashboard.reporting.status");
  if (status === null) return null;

  const Icon = STATUS_ICON[status];
  return (
    <Badge variant={STATUS_BADGE_VARIANT[status]} className="inline-flex items-center gap-1">
      <Icon className="h-3 w-3" strokeWidth={2} />
      {t(status)}
    </Badge>
  );
}
