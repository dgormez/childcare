"use client";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { CheckCircle2, AlertTriangle, FileEdit, Clock, RefreshCw } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Badge } from "../ui/badge";
import type { InvoiceResponse } from "../../lib/types";

interface InvoiceTableProps {
  invoices: InvoiceResponse[];
}

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : "—";
}

/** platform-rules.md director-web: full-row click affordance rather than a small inline icon
 * button, since the row itself represents one record (mirrors LocationsTable/IncidentReportsTable). */
export function InvoiceTable({ invoices }: InvoiceTableProps) {
  const t = useTranslations("invoices");
  const router = useRouter();

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnAmount")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead>{t("columnDueDate")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {invoices.map((invoice) => (
          <TableRow key={invoice.id} className="cursor-pointer" onClick={() => router.push(`/invoices/${invoice.id}`)}>
            <TableCell className="font-medium">{invoice.childName}</TableCell>
            <TableCell className="tabular-nums">{formatCents(invoice.totalCents)}</TableCell>
            <TableCell>
              {invoice.status === "draft" && (
                <Badge variant="neutral" className="inline-flex items-center gap-1">
                  <FileEdit className="h-3 w-3" strokeWidth={2} />
                  {t("statusDraft")}
                </Badge>
              )}
              {invoice.status === "sent" && !invoice.isOverdue && (
                <Badge variant="neutral" className="inline-flex items-center gap-1">
                  <Clock className="h-3 w-3" strokeWidth={2} />
                  {t("statusSent")}
                </Badge>
              )}
              {invoice.status === "sent" && invoice.isOverdue && (
                <Badge variant="danger" className="inline-flex items-center gap-1">
                  <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                  {t("statusOverdue")}
                </Badge>
              )}
              {invoice.status === "pendingdebit" && (
                <Badge variant="info" className="inline-flex items-center gap-1">
                  <RefreshCw className="h-3 w-3" strokeWidth={2} />
                  {t("statusPendingDebit")}
                </Badge>
              )}
              {invoice.status === "paid" && (
                <Badge variant="success" className="inline-flex items-center gap-1">
                  <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
                  {t("statusPaid")}
                </Badge>
              )}
            </TableCell>
            <TableCell className="tabular-nums">{formatDate(invoice.dueDate)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
