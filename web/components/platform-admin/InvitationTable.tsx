"use client";
import { useTranslations } from "next-intl";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Button } from "../ui/button";
import { InvitationStatusBadge } from "./InvitationStatusBadge";
import type { PlatformAdminInvitationResponse } from "../../lib/types";

interface InvitationTableProps {
  invitations: PlatformAdminInvitationResponse[];
  onResend: (invitation: PlatformAdminInvitationResponse) => void;
  onRevoke: (invitation: PlatformAdminInvitationResponse) => void;
}

function formatDateTime(value: string | null): string {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}

// FR-007: resend/revoke are offered for anything not yet Accepted (Pending, Expired, or an
// already-Revoked row) — the row actions gate purely on status, matching the backend's own rule.
export function InvitationTable({ invitations, onResend, onRevoke }: InvitationTableProps) {
  const t = useTranslations("platformAdmin.invitations");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnEmail")}</TableHead>
          <TableHead>{t("columnNote")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead>{t("columnCreated")}</TableHead>
          <TableHead className="text-right">{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {invitations.map((invitation) => {
          const actionable = invitation.status !== "accepted";
          return (
            <TableRow key={invitation.id}>
              <TableCell className="font-medium">{invitation.email}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">
                {invitation.organisationNameNote ?? "—"}
              </TableCell>
              <TableCell>
                <InvitationStatusBadge status={invitation.status} />
                {invitation.status === "revoked" && invitation.revokedByEmail && (
                  <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark">
                    {t("revokedBy", { email: invitation.revokedByEmail, date: formatDateTime(invitation.revokedAt) })}
                  </p>
                )}
              </TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">
                {formatDateTime(invitation.createdAt)}
              </TableCell>
              <TableCell className="text-right">
                {actionable && (
                  <div className="flex items-center justify-end gap-1">
                    <Button variant="ghost" size="sm" onClick={() => onResend(invitation)}>
                      {t("resend")}
                    </Button>
                    <Button variant="destructive" size="sm" onClick={() => onRevoke(invitation)}>
                      {t("revoke")}
                    </Button>
                  </div>
                )}
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
