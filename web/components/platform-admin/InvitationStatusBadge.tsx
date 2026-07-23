import { useTranslations } from "next-intl";
import { Clock, CheckCircle2, CalendarX, Ban } from "lucide-react";
import { Badge, type BadgeProps } from "../ui/badge";
import type { PlatformAdminInvitationStatus } from "../../lib/types";

// design-system.md's Status Indicators rule: color paired with an icon, never color alone.
// Pending reuses the fixed warning→clock pairing; accepted reuses success→check-circle.
// Expired/revoked have no prior fixed pairing in design-system.md, so a distinct icon is
// chosen for each to stay visually unambiguous from pending/accepted.
const STATUS_VARIANT: Record<PlatformAdminInvitationStatus, BadgeProps["variant"]> = {
  pending: "warning",
  accepted: "success",
  expired: "neutral",
  revoked: "danger",
};

const STATUS_ICON: Record<PlatformAdminInvitationStatus, typeof Clock> = {
  pending: Clock,
  accepted: CheckCircle2,
  expired: CalendarX,
  revoked: Ban,
};

export function InvitationStatusBadge({ status }: { status: PlatformAdminInvitationStatus }) {
  const t = useTranslations("platformAdmin.invitations.status");
  const Icon = STATUS_ICON[status];

  return (
    <Badge variant={STATUS_VARIANT[status]} className="inline-flex items-center gap-1">
      <Icon className="h-3 w-3" strokeWidth={2} />
      {t(status)}
    </Badge>
  );
}
