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
import type { ParentInvoiceListEntry } from "../types";

export type InvoicesLoadResult =
  | { status: "loaded"; invoices: ParentInvoiceListEntry[] }
  | { status: "unavailable" };

let cache: ParentInvoiceListEntry[] | null = null;

export async function getInvoices(): Promise<InvoicesLoadResult> {
  try {
    const result = await apiClient.GET("/api/parent/invoices");
    if (!result.response.ok) throw new Error("invoices_load_failed");
    const invoices = result.data as unknown as ParentInvoiceListEntry[];
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

// Feature 030 (US3) — combined family invoice PDF (one document covering every sibling in the
// group), mirrors downloadInvoicePdf's download-then-share shape exactly.
export async function downloadFamilyInvoicePdf(familyGroupId: string) {
  const token = useStore.getState().auth?.accessToken;
  const destination = new Directory(Paths.cache, "invoices");
  destination.create({ intermediates: true, idempotent: true });
  const url = `${getApiBaseUrl()}/api/parent/invoices/family/${familyGroupId}/pdf`;
  return File.downloadFileAsync(url, new File(destination, `family-${familyGroupId}.pdf`), {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    idempotent: true,
  });
}
