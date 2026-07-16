/**
 * payments.ts — feature 014a. Mirrors services/invoices.ts's fetch pattern; downloadBetalingsbewijs
 * reuses downloadInvoicePdf's expo-file-system/expo-sharing precedent exactly (014).
 */
import { Directory, File, Paths } from "expo-file-system";
import { apiClient, getApiBaseUrl } from "./apiClient";
import { useStore } from "../store/useStore";
import type { PaymentLinkResponse, PaymentStatusResponse } from "../types";

export type CreatePaymentLinkResult =
  | { status: "created"; checkoutUrl: string }
  | { status: "not_connected" }
  | { status: "error" };

export async function createPaymentLink(invoiceId: string): Promise<CreatePaymentLinkResult> {
  const result = await apiClient.POST("/api/parent/invoices/{id}/payment-link", { params: { path: { id: invoiceId } } });
  if (result.response.ok) {
    const { checkoutUrl } = result.data as unknown as PaymentLinkResponse;
    return { status: "created", checkoutUrl };
  }
  if (result.response.status === 422) return { status: "not_connected" };
  return { status: "error" };
}

export async function getPaymentStatus(invoiceId: string): Promise<PaymentStatusResponse | null> {
  const result = await apiClient.GET("/api/parent/invoices/{id}/payment-status", { params: { path: { id: invoiceId } } });
  if (!result.response.ok) return null;
  return result.data as unknown as PaymentStatusResponse;
}

// See downloadInvoicePdf's comment (services/invoices.ts) for why no explicit return-type
// annotation.
export async function downloadBetalingsbewijs(invoiceId: string) {
  const token = useStore.getState().auth?.accessToken;
  const destination = new Directory(Paths.cache, "receipts");
  destination.create({ intermediates: true, idempotent: true });
  const url = `${getApiBaseUrl()}/api/parent/invoices/${invoiceId}/betalingsbewijs`;
  return File.downloadFileAsync(url, new File(destination, `${invoiceId}-betalingsbewijs.pdf`), {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    idempotent: true,
  });
}
