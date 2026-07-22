"use client";
import { useTranslations } from "next-intl";
import { CheckCircle2, Clock, AlertTriangle, RotateCcw } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import type { CodaTransactionResponse } from "../../lib/types";

interface CodaTransactionTableProps {
  transactions: CodaTransactionResponse[];
  onConfirm: (id: string) => void;
  onReject: (id: string) => void;
  onReview: (id: string) => void;
  busyId: string | null;
}

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

const NEEDS_REVIEW_TYPES: CodaTransactionResponse["matchType"][] = ["unmatched", "duplicate", "closedinvoice"];

/** design-system.md: badge+icon pairing, one fixed meaning per icon — success/check-circle for
 * an applied match, warning/clock for something pending a director action, danger/alert-triangle
 * for something needing manual investigation, neutral for informational-only (reversal, or a
 * needs-review row already marked handled). */
function StatusBadge({ transaction }: { transaction: CodaTransactionResponse }) {
  const t = useTranslations("invoices.codaReconciliation");

  if (transaction.matchType === "reversal") {
    return (
      <Badge variant="neutral" className="inline-flex items-center gap-1">
        <RotateCcw className="h-3 w-3" strokeWidth={2} />
        {t("matchTypeReversal")}
      </Badge>
    );
  }

  if (transaction.applied) {
    return (
      <Badge variant="success" className="inline-flex items-center gap-1">
        <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
        {t("matchTypeOgm")}
      </Badge>
    );
  }

  if (transaction.matchType === "ogm") {
    // Applied === false with matchType "ogm" only happens for a partial payment (spec.md FR-010)
    return (
      <Badge variant="warning" className="inline-flex items-center gap-1">
        <Clock className="h-3 w-3" strokeWidth={2} />
        {t("partialPaymentLabel", {
          received: formatCents(transaction.matchedInvoice?.receivedCents ?? 0),
          total: formatCents(transaction.matchedInvoice?.totalCents ?? 0),
        })}
      </Badge>
    );
  }

  if (transaction.matchType === "ibanamount") {
    return (
      <Badge variant="warning" className="inline-flex items-center gap-1">
        <Clock className="h-3 w-3" strokeWidth={2} />
        {t("matchTypeIbanAmountPending")}
      </Badge>
    );
  }

  if (transaction.reviewedAt) {
    return (
      <Badge variant="neutral" className="inline-flex items-center gap-1">
        <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
        {t("reviewedLabel")}
      </Badge>
    );
  }

  const label =
    transaction.matchType === "duplicate"
      ? t("matchTypeDuplicate")
      : transaction.matchType === "closedinvoice"
        ? t("matchTypeClosedInvoice")
        : t("matchTypeUnmatched");

  return (
    <Badge variant="danger" className="inline-flex items-center gap-1">
      <AlertTriangle className="h-3 w-3" strokeWidth={2} />
      {label}
    </Badge>
  );
}

export function CodaTransactionTable({ transactions, onConfirm, onReject, onReview, busyId }: CodaTransactionTableProps) {
  const t = useTranslations("invoices.codaReconciliation");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnDate")}</TableHead>
          <TableHead>{t("columnAmount")}</TableHead>
          <TableHead>{t("columnSender")}</TableHead>
          <TableHead>{t("columnCommunication")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {transactions.map((transaction) => (
          <TableRow key={transaction.id}>
            <TableCell className="tabular-nums">{formatDate(transaction.valueDate)}</TableCell>
            <TableCell className="tabular-nums">{formatCents(transaction.amountCents)}</TableCell>
            <TableCell>
              <div className="font-medium">{transaction.senderName || "—"}</div>
              <div className="text-xs text-text-soft dark:text-text-soft-dark">{transaction.senderIbanMasked}</div>
            </TableCell>
            <TableCell className="max-w-xs truncate">{transaction.communication || "—"}</TableCell>
            <TableCell>
              <StatusBadge transaction={transaction} />
            </TableCell>
            <TableCell>
              {transaction.matchType === "ibanamount" && !transaction.applied && (
                <div className="flex items-center gap-2">
                  <Button variant="ghost" size="sm" disabled={busyId === transaction.id} onClick={() => onConfirm(transaction.id)}>
                    {t("confirmButton")}
                  </Button>
                  <Button variant="ghost" size="sm" disabled={busyId === transaction.id} onClick={() => onReject(transaction.id)}>
                    {t("rejectButton")}
                  </Button>
                </div>
              )}
              {NEEDS_REVIEW_TYPES.includes(transaction.matchType) && !transaction.reviewedAt && (
                <Button variant="ghost" size="sm" disabled={busyId === transaction.id} onClick={() => onReview(transaction.id)}>
                  {t("reviewButton")}
                </Button>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
