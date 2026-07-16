"use client";
import { useTranslations } from "next-intl";
import { CheckCircle2, Clock, AlertTriangle } from "lucide-react";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../ui/table";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import type { FiscalAttestationResponse } from "../../lib/types";

interface FiscalAttestationTableProps {
  attestations: FiscalAttestationResponse[];
  onRegenerate: (attestation: FiscalAttestationResponse) => void;
  onDownload: (attestation: FiscalAttestationResponse) => void;
  regeneratingKey: string | null;
}

function formatCents(cents: number): string {
  return (cents / 100).toLocaleString(undefined, { style: "currency", currency: "EUR" });
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : "—";
}

function rowKey(a: FiscalAttestationResponse): string {
  return `${a.childId}:${a.locationId}`;
}

/** platform-rules.md director-web: full-row density per design-system.md's table conventions;
 * per-row regenerate/download actions live in a dedicated column since this table represents
 * eligible (child, location) pairs, not a single navigable record (unlike InvoiceTable). */
export function FiscalAttestationTable({ attestations, onRegenerate, onDownload, regeneratingKey }: FiscalAttestationTableProps) {
  const t = useTranslations("fiscalAttestations");

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("columnChild")}</TableHead>
          <TableHead>{t("columnLocation")}</TableHead>
          <TableHead>{t("columnAmount")}</TableHead>
          <TableHead>{t("columnStatus")}</TableHead>
          <TableHead>{t("columnGeneratedAt")}</TableHead>
          <TableHead>{t("columnActions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {attestations.map((attestation) => (
          <TableRow key={rowKey(attestation)}>
            <TableCell className="font-medium">{attestation.childName}</TableCell>
            <TableCell>{attestation.locationName}</TableCell>
            <TableCell className="tabular-nums">
              {attestation.totalAmountCents !== null ? formatCents(attestation.totalAmountCents) : "—"}
            </TableCell>
            <TableCell>
              {attestation.status === "generated" && (
                <Badge variant="success" className="inline-flex items-center gap-1">
                  <CheckCircle2 className="h-3 w-3" strokeWidth={2} />
                  {t("statusGenerated")}
                </Badge>
              )}
              {attestation.status === "notYetGenerated" && (
                <Badge variant="neutral" className="inline-flex items-center gap-1">
                  <Clock className="h-3 w-3" strokeWidth={2} />
                  {t("statusNotYetGenerated")}
                </Badge>
              )}
              {attestation.status === "failed" && (
                <Badge variant="danger" className="inline-flex items-center gap-1">
                  <AlertTriangle className="h-3 w-3" strokeWidth={2} />
                  {t("statusFailed")}
                </Badge>
              )}
            </TableCell>
            <TableCell className="tabular-nums">{formatDate(attestation.generatedAt)}</TableCell>
            <TableCell>
              <div className="flex items-center gap-2">
                {attestation.status === "generated" && (
                  <Button variant="secondary" onClick={() => onDownload(attestation)}>
                    {t("downloadPdf")}
                  </Button>
                )}
                <Button
                  variant="secondary"
                  onClick={() => onRegenerate(attestation)}
                  disabled={regeneratingKey === rowKey(attestation)}
                >
                  {t("regenerateButton")}
                </Button>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
