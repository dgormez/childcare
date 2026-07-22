import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { InvoiceDetail } from "../components/invoices/InvoiceDetail";
import { apiClient } from "../lib/apiClient";
import type { InvoiceResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { PUT: vi.fn(), POST: vi.fn() },
  getAccessToken: vi.fn(() => "token"),
}));

function makeInvoice(overrides: Partial<InvoiceResponse> = {}): InvoiceResponse {
  return {
    id: "inv-1",
    childId: "child-1",
    childName: "Emma Peeters",
    contractId: "contract-1",
    locationId: "loc-1",
    locationName: "Sunshine House",
    periodMonth: "2026-07",
    status: "draft",
    isOverdue: false,
    subtotalCents: 45000,
    totalCents: 45000,
    lineItems: {
      presentDays: 15,
      unjustifiedAbsentDays: 0,
      dailyRateCents: 3000,
      closureDaysExcluded: 0,
      daysMin5u: 0,
      daysMin11u: 15,
      extraCharges: [],
    },
    ogmReference: "",
    dueDate: null,
    sentAt: null,
    paidAt: null,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: "2026-07-01T00:00:00Z",
    sepaBatchId: null,
    sepaReturnReason: null,
    ...overrides,
  };
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function renderDetail(invoice: InvoiceResponse, onUpdated = vi.fn()) {
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <InvoiceDetail invoice={invoice} onUpdated={onUpdated} />
    </NextIntlClientProvider>,
  );
  return onUpdated;
}

beforeEach(() => {
  vi.mocked(apiClient.PUT).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("InvoiceDetail", () => {
  it("adds an extra charge, saves it, and reflects the updated total via onUpdated", async () => {
    const invoice = makeInvoice();
    const updated = makeInvoice({
      totalCents: 47000,
      lineItems: { ...invoice.lineItems, extraCharges: [{ label: "Late pickup", amountCents: 2000 }] },
    });
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onUpdated = renderDetail(invoice);

    await userEvent.type(screen.getByLabelText("Label"), "Late pickup");
    await userEvent.type(screen.getByLabelText("Amount"), "20");
    await userEvent.click(screen.getByRole("button", { name: "Add extra charge" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(onUpdated).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/invoices/{id}/extra-charges",
      expect.objectContaining({
        params: { path: { id: "inv-1" } },
        body: { extraCharges: [{ label: "Late pickup", amountCents: 2000 }] },
      }),
    );
  });

  it("sends the invoice and reflects the updated status via onUpdated", async () => {
    const invoice = makeInvoice();
    const sent = makeInvoice({ status: "sent", sentAt: "2026-07-15T00:00:00Z", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++" });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse([sent]) as never);
    const onUpdated = renderDetail(invoice);

    await userEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(onUpdated).toHaveBeenCalledWith(sent));
    expect(apiClient.POST).toHaveBeenCalledWith("/api/invoices/send", { body: { invoiceIds: ["inv-1"] } });
    expect(await screen.findByText("Invoice sent.")).toBeInTheDocument();
  });

  it("marks a sent invoice as paid and updates the status via onUpdated", async () => {
    const invoice = makeInvoice({ status: "sent", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++" });
    const paid = makeInvoice({ status: "paid", dueDate: "2026-07-29", paidAt: "2026-07-20T00:00:00Z" });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(paid) as never);
    const onUpdated = renderDetail(invoice);

    await userEvent.click(screen.getByRole("button", { name: "Mark as paid" }));

    await waitFor(() => expect(onUpdated).toHaveBeenCalledWith(paid));
    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/invoices/{id}/mark-paid",
      expect.objectContaining({ params: { path: { id: "inv-1" } } }),
    );
    expect(await screen.findByText("Invoice marked as paid.")).toBeInTheDocument();
  });

  it("shows the regenerate action for a draft invoice", () => {
    renderDetail(makeInvoice({ status: "draft" }));
    expect(screen.getByRole("button", { name: "Regenerate" })).toBeInTheDocument();
  });

  it("shows the regenerate action for a sent invoice", () => {
    renderDetail(makeInvoice({ status: "sent", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++" }));
    expect(screen.getByRole("button", { name: "Regenerate" })).toBeInTheDocument();
  });

  it("hides the regenerate action for a paid invoice", () => {
    renderDetail(makeInvoice({ status: "paid", dueDate: "2026-07-29", paidAt: "2026-07-20T00:00:00Z" }));
    expect(screen.queryByRole("button", { name: "Regenerate" })).not.toBeInTheDocument();
  });

  it("regenerates the invoice and reflects the recomputed line items via onUpdated", async () => {
    const invoice = makeInvoice({ status: "sent", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++" });
    const regenerated = makeInvoice({
      status: "sent",
      dueDate: "2026-07-29",
      ogmReference: "+++097/0000/00017+++",
      totalCents: 42000,
      lineItems: { ...invoice.lineItems, presentDays: 14 },
    });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(regenerated) as never);
    const onUpdated = renderDetail(invoice);

    await userEvent.click(screen.getByRole("button", { name: "Regenerate" }));

    await waitFor(() => expect(onUpdated).toHaveBeenCalledWith(regenerated));
    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/invoices/{id}/regenerate",
      expect.objectContaining({ params: { path: { id: "inv-1" } } }),
    );
  });

  // Feature 026 — FR-010.
  it("marks a pending-debit invoice as returned with a reason, and updates the status via onUpdated", async () => {
    const invoice = makeInvoice({ status: "pendingdebit", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++", sepaBatchId: "batch-1" });
    const returned = makeInvoice({ status: "sent", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++", sepaReturnReason: "Insufficient funds" });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(returned) as never);
    const onUpdated = renderDetail(invoice);

    await userEvent.type(screen.getByLabelText("Reason (e.g. insufficient funds)"), "Insufficient funds");
    await userEvent.click(screen.getByRole("button", { name: "Mark as returned" }));

    await waitFor(() => expect(onUpdated).toHaveBeenCalledWith(returned));
    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/invoices/{id}/mark-sepa-returned",
      expect.objectContaining({ params: { path: { id: "inv-1" } }, body: { reason: "Insufficient funds" } }),
    );
  });

  it("disables the mark-as-returned action until a reason is entered", () => {
    const invoice = makeInvoice({ status: "pendingdebit", dueDate: "2026-07-29", ogmReference: "+++097/0000/00017+++" });
    renderDetail(invoice);

    expect(screen.getByRole("button", { name: "Mark as returned" })).toBeDisabled();
  });
});
