/**
 * milestones.ts — feature 016, US3/US4. Fetch pattern mirrors services/invoices.ts's
 * fetch-then-cache-fallback shape. Download mirrors downloadInvoicePdf exactly (research.md
 * R4 — the milestone portfolio PDF is rendered on-demand, never persisted, so this is a direct
 * authenticated stream download, not a signed-URL fetch like fiscalAttestations.ts).
 */
import { Directory, File, Paths } from "expo-file-system";
import { apiClient, getApiBaseUrl } from "./apiClient";
import { useStore } from "../store/useStore";
import type { DevelopmentalDomainResponse } from "../types";

export type MilestonePortfolioLoadResult =
  | { status: "loaded"; domains: DevelopmentalDomainResponse[] }
  | { status: "unavailable" };

export async function getMilestonePortfolio(childId: string): Promise<MilestonePortfolioLoadResult> {
  try {
    const result = await apiClient.GET("/api/parent/children/{childId}/milestone-portfolio", {
      params: { path: { childId } },
    });
    if (!result.response.ok) throw new Error("milestone_portfolio_load_failed");
    const data = result.data as unknown as { domains: DevelopmentalDomainResponse[] };
    return { status: "loaded", domains: data.domains };
  } catch {
    return { status: "unavailable" };
  }
}

// No explicit return-type annotation — see invoices.ts's downloadInvoicePdf for why.
export async function downloadMilestonePortfolioPdf(childId: string) {
  const token = useStore.getState().auth?.accessToken;
  const destination = new Directory(Paths.cache, "milestone-portfolios");
  destination.create({ intermediates: true, idempotent: true });
  const url = `${getApiBaseUrl()}/api/parent/children/${childId}/milestone-portfolio/pdf`;
  return File.downloadFileAsync(url, new File(destination, `${childId}.pdf`), {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    idempotent: true,
  });
}
