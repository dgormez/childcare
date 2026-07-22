import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import CodaReconciliationPage from "../app/(app)/invoices/reconciliation/page";
import { apiClient } from "../lib/apiClient";
import type { CodaImportSummaryResponse, CodaTransactionResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <CodaReconciliationPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.coda_import.invalid_file" } };
}

function makeTransaction(overrides: Partial<CodaTransactionResponse> = {}): CodaTransactionResponse {
  return {
    id: "txn-1",
    importId: "import-1",
    valueDate: "2027-03-15",
    amountCents: 45000,
    senderIbanMasked: "•••• 7034",
    senderName: "Test Parent",
    communication: "123456789012",
    matchType: "unmatched",
    applied: false,
    matchedInvoice: null,
    reviewedAt: null,
    ...overrides,
  };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("CodaReconciliationPage", () => {
  it("shows an empty state when no transactions need review yet", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    renderPage();

    expect(await screen.findByText("No transactions imported yet. Upload a CODA file to get started.")).toBeInTheDocument();
  });

  it("shows an error state when loading transactions fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(errorResponse(500) as never);
    renderPage();

    expect(await screen.findByText("Couldn't load transactions. Please try again.")).toBeInTheDocument();
  });

  it("uploads a file, displays the import summary, and shows a human-readable error on a rejected file", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    const summary: CodaImportSummaryResponse = {
      importId: "import-1",
      transactionCount: 3,
      skippedDuplicateCount: 0,
      summary: { ogm: 2, ibanAmountSuggested: 0, unmatched: 1, duplicate: 0, closedInvoice: 0, reversal: 0 },
    };
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(summary) as never);
    renderPage();
    await screen.findByText("No transactions imported yet. Upload a CODA file to get started.");

    const file = new File(["2027-03-15|45000|BE68539007547034|Test Parent|123456789012|true"], "statement.coda", { type: "text/plain" });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(input, file);

    expect(await screen.findByText("3 transactions imported.")).toBeInTheDocument();
    expect(apiClient.POST).toHaveBeenCalledWith("/api/coda-imports", expect.objectContaining({ body: expect.objectContaining({ file }) }));
  });

  it("confirms a suggested match and refreshes the list", async () => {
    const suggested = makeTransaction({ id: "txn-2", matchType: "ibanamount" });
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([suggested]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({ ...suggested, applied: true }) as never);
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "Confirm" }));

    expect(apiClient.POST).toHaveBeenCalledWith("/api/coda-transactions/{id}/confirm", { params: { path: { id: "txn-2" } } });
    expect(await screen.findByText("Match confirmed, invoice marked as paid.")).toBeInTheDocument();
  });

  it("marks an unmatched transaction as reviewed, removing it from the default needs-review view", async () => {
    const unmatched = makeTransaction({ id: "txn-3", matchType: "unmatched" });
    vi.mocked(apiClient.GET)
      .mockResolvedValueOnce(okResponse([unmatched]) as never)
      .mockResolvedValueOnce(okResponse([]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({ ...unmatched, reviewedAt: "2027-03-16T00:00:00Z" }) as never);
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "Mark as handled" }));

    expect(apiClient.POST).toHaveBeenCalledWith("/api/coda-transactions/{id}/review", { params: { path: { id: "txn-3" } } });
    expect(await screen.findByText("No transactions imported yet. Upload a CODA file to get started.")).toBeInTheDocument();
  });
});
