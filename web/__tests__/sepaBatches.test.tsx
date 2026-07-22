import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import SepaBatchesPage from "../app/(app)/invoices/sepa-batches/page";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse, SepaBatchEligibilityResponse, SepaBatchResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
  getAccessToken: () => "test-token",
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <SepaBatchesPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

const location: LocationResponse = { id: "loc-1", name: "Main" } as LocationResponse;

const eligibleOnly: SepaBatchEligibilityResponse = {
  creditorConfigured: true,
  eligible: [{ invoiceId: "inv-1", childName: "Emma Peeters", totalCents: 45000, debtorName: "Jan Peeters" }],
  excluded: [{ invoiceId: "inv-2", childName: "Lucas Janssens", totalCents: 30000, reason: "NoMandate" }],
};

const noBatches: SepaBatchResponse[] = [];

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  global.fetch = vi.fn();
});

describe("SepaBatchesPage", () => {
  it("shows a message when the creditor identifier/bank account aren't configured", async () => {
    vi.mocked(apiClient.GET)
      .mockResolvedValueOnce(okResponse([location]) as never)
      .mockResolvedValueOnce(okResponse({ creditorConfigured: false, eligible: [], excluded: [] }) as never)
      .mockResolvedValueOnce(okResponse(noBatches) as never);
    renderPage();

    expect(
      await screen.findByText(
        "Configure the creditor identifier (organisation settings) and the bank account number (location settings) before generating a SEPA batch.",
      ),
    ).toBeInTheDocument();
  });

  it("shows an empty state when the creditor is configured but no invoices exist either way", async () => {
    vi.mocked(apiClient.GET)
      .mockResolvedValueOnce(okResponse([location]) as never)
      .mockResolvedValueOnce(okResponse({ creditorConfigured: true, eligible: [], excluded: [] }) as never)
      .mockResolvedValueOnce(okResponse(noBatches) as never);
    renderPage();

    expect(await screen.findByText("No sent invoices for this location and month.")).toBeInTheDocument();
  });

  it("splits invoices into eligible and excluded lists", async () => {
    vi.mocked(apiClient.GET)
      .mockResolvedValueOnce(okResponse([location]) as never)
      .mockResolvedValueOnce(okResponse(eligibleOnly) as never)
      .mockResolvedValueOnce(okResponse(noBatches) as never);
    renderPage();

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(await screen.findByText("Lucas Janssens")).toBeInTheDocument();
    expect(await screen.findByText("No mandate")).toBeInTheDocument();
  });

  it("generates a batch and shows a success notice", async () => {
    vi.mocked(apiClient.GET)
      .mockResolvedValueOnce(okResponse([location]) as never)
      .mockResolvedValueOnce(okResponse(eligibleOnly) as never)
      .mockResolvedValueOnce(okResponse(noBatches) as never)
      .mockResolvedValueOnce(okResponse(eligibleOnly) as never)
      .mockResolvedValueOnce(okResponse(noBatches) as never);
    vi.mocked(global.fetch).mockResolvedValue({
      ok: true,
      blob: async () => new Blob(["<xml/>"], { type: "application/xml" }),
    } as Response);
    renderPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Generate SEPA batch" }));

    expect(await screen.findByText("SEPA batch generated for 1 invoice(s).")).toBeInTheDocument();
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("/api/locations/loc-1/sepa-batches"),
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("shows a human-readable error when generation fails", async () => {
    vi.mocked(apiClient.GET)
      .mockResolvedValueOnce(okResponse([location]) as never)
      .mockResolvedValueOnce(okResponse(eligibleOnly) as never)
      .mockResolvedValueOnce(okResponse(noBatches) as never);
    vi.mocked(global.fetch).mockResolvedValue({
      ok: false,
      json: async () => ({ errorKey: "errors.sepa_batch.execution_date_too_soon" }),
    } as Response);
    renderPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Generate SEPA batch" }));

    expect(await screen.findByText("Choose an execution date at least one business day in the future.")).toBeInTheDocument();
  });
});
