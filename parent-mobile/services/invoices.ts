/**
 * invoices.ts — feature 014, US3. Fetch pattern mirrors services/menu.ts's fetch-then-cache-
 * fallback shape (same in-memory-only, session-lifetime cache — parent-mobile has no persistent
 * offline store, spec.md Assumptions: parents are expected to have network access).
 *
 * No prior PDF-download precedent exists anywhere in this monorepo's mobile apps (director-web's
 * equivalent uses a browser blob download, which has no RN equivalent) — downloadInvoicePdf is
 * the first use of expo-file-system/expo-sharing, added specifically for this feature.
 */
import { Directory, File, Paths } from "expo-file-system";
import { apiClient, getApiBaseUrl } from "./apiClient";
import { useStore } from "../store/useStore";
import type { ParentInvoiceEntry } from "../types";

export type InvoicesLoadResult =
  | { status: "loaded"; invoices: ParentInvoiceEntry[] }
  | { status: "unavailable" };

let cache: ParentInvoiceEntry[] | null = null;

export async function getInvoices(): Promise<InvoicesLoadResult> {
  try {
    const result = await apiClient.GET("/api/parent/invoices");
    if (!result.response.ok) throw new Error("invoices_load_failed");
    const invoices = result.data as unknown as ParentInvoiceEntry[];
    cache = invoices;
    return { status: "loaded", invoices };
  } catch {
    return cache ? { status: "loaded", invoices: cache } : { status: "unavailable" };
  }
}

// No explicit return-type annotation: File.downloadFileAsync's static signature is inherited
// from expo-file-system's native FileSystemFile base, a structurally narrower "File" than the
// wrapper class imported above — annotating `Promise<File>` here fails to compile even though
// the value itself is a genuine wrapper File instance at runtime (its `.uri` is all callers need).
export async function downloadInvoicePdf(invoiceId: string) {
  const token = useStore.getState().auth?.accessToken;
  const destination = new Directory(Paths.cache, "invoices");
  destination.create({ intermediates: true, idempotent: true });
  const url = `${getApiBaseUrl()}/api/parent/invoices/${invoiceId}/pdf`;
  return File.downloadFileAsync(url, new File(destination, `${invoiceId}.pdf`), {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    idempotent: true,
  });
}
