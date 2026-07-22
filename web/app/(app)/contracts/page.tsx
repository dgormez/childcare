"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { FileSignature } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { ContractsTable } from "../../../components/ContractsTable";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { ContractSummaryResponse, SignedContractDownloadUrlResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

const KNOWN_ERROR_KEYS = new Set([
  "errors.contract_signing.no_contact_email",
  "errors.contract_signing.creditor_id_not_configured",
  "errors.contract.not_draft",
  "errors.contract.already_signed",
]);

/**
 * Feature 024-esignature (User Story 2). Replaces the NotYetAvailable stub — spec.md's own
 * Assumptions note this feature builds only the minimal contract-signing view/actions it needs,
 * not a full contract creation/management UI (which stays out of scope, per the 007a/013c/023
 * precedent cited there).
 */
export default function ContractsPage() {
  const t = useTranslations("contracts");
  const [contracts, setContracts] = useState<ContractSummaryResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [sendingId, setSendingId] = useState<string | null>(null);
  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/contracts");
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setContracts(result.data as unknown as ContractSummaryResponse[]);
    setState("loaded");
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function sendInvitation(contractId: string) {
    setSendingId(contractId);
    setNotice("");
    const result = await apiClient.POST("/api/contracts/{id}/signing-invitation", {
      params: { path: { id: contractId } },
    });
    setSendingId(null);

    if (!result.response.ok) {
      const errorKey = (result.error as { errorKey?: string } | undefined)?.errorKey;
      setNotice(errorKey && KNOWN_ERROR_KEYS.has(errorKey) ? t(`errors.${errorKeyToMessageKey(errorKey)}`) : t("errors.generic"));
      return;
    }

    setNotice(t("sendSuccess"));
    await load();
  }

  async function revokeMandate(contractId: string) {
    setRevokingId(contractId);
    setNotice("");
    const result = await apiClient.POST("/api/contracts/{id}/revoke-sepa-mandate", {
      params: { path: { id: contractId } },
    });
    setRevokingId(null);
    setNotice(result.response.ok ? t("revokeMandateSuccess") : t("revokeMandateError"));
    await load();
  }

  async function viewSignedPdf(contractId: string) {
    const result = await apiClient.GET("/api/contracts/{id}/signed-pdf-url", {
      params: { path: { id: contractId } },
    });
    if (!result.response.ok) return;
    const { downloadUrl } = result.data as unknown as SignedContractDownloadUrlResponse;
    window.open(downloadUrl, "_blank", "noopener,noreferrer");
  }

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>

      {notice && <p className="mb-4 text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      {state === "loading" && (
        <div className="h-64 animate-pulse rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />
      )}

      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}

      {state === "loaded" && contracts.length === 0 && <EmptyState icon={FileSignature} message={t("empty")} />}

      {state === "loaded" && contracts.length > 0 && (
        <ContractsTable
          contracts={contracts}
          sendingId={sendingId}
          revokingId={revokingId}
          onSend={sendInvitation}
          onRevokeMandate={revokeMandate}
          onViewSignedPdf={viewSignedPdf}
        />
      )}
    </div>
  );
}

function errorKeyToMessageKey(errorKey: string): string {
  switch (errorKey) {
    case "errors.contract_signing.no_contact_email":
      return "noContactEmail";
    case "errors.contract_signing.creditor_id_not_configured":
      return "creditorIdNotConfigured";
    case "errors.contract.not_draft":
      return "notDraft";
    case "errors.contract.already_signed":
      return "alreadySigned";
    default:
      return "generic";
  }
}
