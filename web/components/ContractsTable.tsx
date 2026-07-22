"use client";
import { useTranslations } from "next-intl";
import { AlertTriangle, CheckCircle2, Clock } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "./ui/table";
import { Badge } from "./ui/badge";
import { Button } from "./ui/button";
import type { ContractSummaryResponse, ContractSigningStatus } from "../lib/types";

interface ContractsTableProps {
  contracts: ContractSummaryResponse[];
  sendingId: string | null;
  revokingId: string | null;
  onSend: (contractId: string) => void;
  onRevokeMandate: (contractId: string) => void;
  onViewSignedPdf: (contractId: string) => void;
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function formatRate(cents: number): string {
  return (cents / 100).toFixed(2);
}

/** design-system.md: every semantic badge pairs its color with an icon, never color alone. */
function SigningStatusBadge({ status }: { status: ContractSigningStatus }) {
  const t = useTranslations("contracts.signingStatus");

  if (status === "pending") {
    return (
      <Badge variant="warning" className="inline-flex items-center gap-1.5">
        <Clock className="h-3 w-3" strokeWidth={2} />
        {t("pending")}
      </Badge>
    );
  }
  if (status === "expired") {
    return (
      <Badge variant="danger" className="inline-flex items-center gap-1.5">
        <AlertTriangle className="h-3 w-3" strokeWidth={2} />
        {t("expired")}
      </Badge>
    );
  }
  if (status === "signed") {
    return (
      <Badge variant="success" className="inline-flex items-center gap-1.5">
        <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
        {t("signed")}
      </Badge>
    );
  }
  return <Badge variant="neutral">{t("notsent")}</Badge>;
}

/** platform-rules.md director-web: high-density table; a row action (send/resend/view PDF)
 * uses the "ghost" button variant designed for this exact row-action convention. */
export function ContractsTable({ contracts, sendingId, revokingId, onSend, onRevokeMandate, onViewSignedPdf }: ContractsTableProps) {
  const t = useTranslations("contracts");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columns.child")}</TableHead>
          <TableHead>{t("columns.location")}</TableHead>
          <TableHead>{t("columns.startDate")}</TableHead>
          <TableHead>{t("columns.dailyRate")}</TableHead>
          <TableHead>{t("columns.status")}</TableHead>
          <TableHead>{t("columns.signingStatus")}</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {contracts.map((contract) => {
          const canSend = contract.status === "draft" && contract.signingStatus === "notsent";
          const canResend = contract.signingStatus === "pending" || contract.signingStatus === "expired";
          const isSending = sendingId === contract.id;

          return (
            <TableRow key={contract.id}>
              <TableCell className="font-medium">{contract.childName}</TableCell>
              <TableCell className="text-text-soft dark:text-text-soft-dark">{contract.locationName}</TableCell>
              <TableCell className="tabular-nums">{formatDate(contract.startDate)}</TableCell>
              <TableCell className="tabular-nums">{formatRate(contract.dailyRateCents)}</TableCell>
              <TableCell>
                <Badge variant="neutral">{t(`status.${contract.status}`)}</Badge>
              </TableCell>
              <TableCell>
                <SigningStatusBadge status={contract.signingStatus} />
                {contract.signingStatus === "signed" && contract.signedAt && (
                  <p className="mt-1 text-xs text-text-soft dark:text-text-soft-dark">
                    {t("signedOnLabel", { date: formatDate(contract.signedAt) })}
                  </p>
                )}
              </TableCell>
              <TableCell>
                {(canSend || canResend) && (
                  <Button variant="ghost" size="sm" disabled={isSending} onClick={() => onSend(contract.id)}>
                    {isSending ? t("sending") : canSend ? t("sendAction") : t("resendAction")}
                  </Button>
                )}
                {contract.signingStatus === "signed" && (
                  <Button variant="ghost" size="sm" onClick={() => onViewSignedPdf(contract.id)}>
                    {t("viewSignedPdf")}
                  </Button>
                )}
                {contract.mandateStatus === "signed" && (
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={revokingId === contract.id}
                    onClick={() => onRevokeMandate(contract.id)}
                  >
                    {revokingId === contract.id ? t("revokingMandate") : t("revokeMandateAction")}
                  </Button>
                )}
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
