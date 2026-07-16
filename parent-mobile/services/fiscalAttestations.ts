/**
 * fiscalAttestations.ts — feature 015, US2. Fetch pattern mirrors services/invoices.ts's
 * fetch-then-cache-fallback shape (same in-memory-only, session-lifetime cache — parent-mobile
 * has no persistent offline store).
 *
 * Unlike invoices.ts's downloadInvoicePdf (which streams directly from the API, since 014's
 * invoice PDFs are rendered on-demand and never persisted), a fiscal attestation's PDF is
 * persisted to GCS and served only via a time-limited signed URL (research.md R1) — so
 * downloading here is a two-step fetch: get the signed URL from the API, then download from
 * that URL directly (no Authorization header — the signature itself authorizes the request).
 */
import { Directory, File, Paths } from "expo-file-system";
import { apiClient } from "./apiClient";
import type { FiscalAttestationResponse } from "../types";

export type FiscalAttestationsLoadResult =
  | { status: "loaded"; attestations: FiscalAttestationResponse[] }
  | { status: "unavailable" };

let cache: FiscalAttestationResponse[] | null = null;

export async function getFiscalAttestations(): Promise<FiscalAttestationsLoadResult> {
  try {
    const result = await apiClient.GET("/api/parent/fiscal-attestations");
    if (!result.response.ok) throw new Error("fiscal_attestations_load_failed");
    const attestations = result.data as unknown as FiscalAttestationResponse[];
    cache = attestations;
    return { status: "loaded", attestations };
  } catch {
    return cache ? { status: "loaded", attestations: cache } : { status: "unavailable" };
  }
}

// No explicit return-type annotation — see invoices.ts's downloadInvoicePdf for why.
export async function downloadFiscalAttestationPdf(attestationId: string) {
  const urlResult = await apiClient.GET("/api/parent/fiscal-attestations/{id}/download-url", {
    params: { path: { id: attestationId } },
  });
  if (!urlResult.response.ok) throw new Error("fiscal_attestation_download_url_failed");
  const { downloadUrl } = urlResult.data as unknown as { downloadUrl: string };

  const destination = new Directory(Paths.cache, "fiscal-attestations");
  destination.create({ intermediates: true, idempotent: true });
  return File.downloadFileAsync(downloadUrl, new File(destination, `${attestationId}.pdf`), {
    idempotent: true,
  });
}
