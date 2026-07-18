import { CheckCircle, Clock, AlertTriangle, type LucideIcon } from "lucide-react";
import type { OccupancyStatus } from "../../lib/types";

/** FR-018: fixed colour→icon pairing — never colour alone. */
export const STATUS_ICON: Record<OccupancyStatus, LucideIcon> = {
  green: CheckCircle,
  amber: Clock,
  red: AlertTriangle,
};

export const STATUS_BADGE_VARIANT: Record<OccupancyStatus, "success" | "warning" | "danger"> = {
  green: "success",
  amber: "warning",
  red: "danger",
};
