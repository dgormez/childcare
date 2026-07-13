import { AlertTriangle, AlertCircle, Circle } from "lucide-react";
import { useTranslations } from "next-intl";
import { Badge } from "../ui/badge";
import type { BadgeProps } from "../ui/badge";

export type AllergySeverityWireValue = "severe" | "mild_moderate" | "none";

// FR-007: severity is never conveyed by color alone — each level gets its own icon *shape*
// (not just a recolored copy of the same shape), so the distinction survives grayscale print
// (SC-003) where color disappears entirely.
const SEVERITY_ICON: Record<AllergySeverityWireValue, typeof AlertTriangle> = {
  severe: AlertTriangle,
  mild_moderate: AlertCircle,
  none: Circle,
};

const SEVERITY_VARIANT: Record<AllergySeverityWireValue, BadgeProps["variant"]> = {
  severe: "danger",
  mild_moderate: "warning",
  none: "neutral",
};

interface AllergySeverityBadgeProps {
  severity: AllergySeverityWireValue;
}

export function AllergySeverityBadge({ severity }: AllergySeverityBadgeProps) {
  const t = useTranslations("mealList");
  const Icon = SEVERITY_ICON[severity];

  return (
    <Badge variant={SEVERITY_VARIANT[severity]} className="inline-flex items-center gap-1">
      <Icon className="h-3 w-3" strokeWidth={2} />
      {t(`allergySeverity.${severity}`)}
    </Badge>
  );
}
