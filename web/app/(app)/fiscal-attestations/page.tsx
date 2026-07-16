"use client";
import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { FileCheck2 } from "lucide-react";
import { apiClient } from "../../../lib/apiClient";
import { Button } from "../../../components/ui/button";
import { FiscalAttestationTable } from "../../../components/fiscal-attestations/FiscalAttestationTable";
import { EmptyState } from "../../../components/EmptyState";
import { ErrorState } from "../../../components/ErrorState";
import type { FiscalAttestationResponse } from "../../../lib/types";

type LoadState = "loading" | "loaded" | "error";

export default function FiscalAttestationsPage() {
  const t = useTranslations("fiscalAttestations");
  const [taxYear, setTaxYear] = useState(new Date().getFullYear());
  const [attestations, setAttestations] = useState<FiscalAttestationResponse[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [generating, setGenerating] = useState(false);
  const [regeneratingKey, setRegeneratingKey] = useState<string | null>(null);
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    setState("loading");
    const result = await apiClient.GET("/api/fiscal-attestations", { params: { query: { taxYear } } });
    if (!result.response.ok) {
      setState("error");
      return;
    }
    setAttestations(result.data as unknown as FiscalAttestationResponse[]);
    setState("loaded");
  }, [taxYear]);

  useEffect(() => {
    load();
  }, [load]);

  async function generate() {
    setGenerating(true);
    setNotice("");
    const result = await apiClient.POST("/api/fiscal-attestations/generate", { body: { taxYear } });
    setGenerating(false);
    if (!result.response.ok) {
      setNotice(t("generateError"));
      return;
    }
    const data = result.data as unknown as { taxYear: number; results: { status: string }[] };
    setNotice(t("generateSuccess", { count: data.results.filter((r) => r.status === "generated").length }));
    await load();
  }

  async function regenerate(attestation: FiscalAttestationResponse) {
    const key = `${attestation.childId}:${attestation.locationId}`;
    setRegeneratingKey(key);
    setNotice("");
    const result = await apiClient.POST("/api/fiscal-attestations/{childId}/{locationId}/{taxYear}/regenerate", {
      params: { path: { childId: attestation.childId, locationId: attestation.locationId, taxYear } },
    });
    setRegeneratingKey(null);
    if (!result.response.ok) {
      setNotice(t("regenerateError"));
      return;
    }
    setNotice(t("regenerateSuccess"));
    await load();
  }

  async function download(attestation: FiscalAttestationResponse) {
    if (!attestation.id) return;
    const result = await apiClient.GET("/api/fiscal-attestations/{id}/download-url", { params: { path: { id: attestation.id } } });
    if (!result.response.ok) {
      setNotice(t("downloadPdfError"));
      return;
    }
    const data = result.data as unknown as { downloadUrl: string };
    window.open(data.downloadUrl, "_blank", "noopener,noreferrer");
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-text dark:text-text-dark">{t("title")}</h1>
        <div className="flex items-center gap-3">
          <input
            type="number"
            value={taxYear}
            onChange={(e) => {
              const parsed = Number(e.target.value);
              if (!Number.isNaN(parsed)) setTaxYear(parsed);
            }}
            aria-label={t("taxYearLabel")}
            className="h-10 w-24 rounded-lg bg-surface-soft px-3 text-sm text-text tabular-nums focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary dark:bg-surface-soft-dark dark:text-text-dark"
          />
          <Button onClick={generate} disabled={generating}>
            {t("generateButton")}
          </Button>
        </div>
      </div>

      {notice && <p className="mb-4 text-sm text-text-soft dark:text-text-soft-dark">{notice}</p>}

      {state === "loading" && <div className="h-64 rounded-xl bg-surface-soft dark:bg-surface-soft-dark" />}
      {state === "error" && <ErrorState message={t("loadError")} retryLabel={t("retry")} onRetry={load} />}
      {state === "loaded" && attestations.length === 0 && <EmptyState icon={FileCheck2} message={t("emptyState")} />}
      {state === "loaded" && attestations.length > 0 && (
        <FiscalAttestationTable
          attestations={attestations}
          onRegenerate={regenerate}
          onDownload={download}
          regeneratingKey={regeneratingKey}
        />
      )}
    </div>
  );
}
