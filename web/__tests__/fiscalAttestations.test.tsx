import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import FiscalAttestationsPage from "../app/(app)/fiscal-attestations/page";
import { apiClient } from "../lib/apiClient";
import type { FiscalAttestationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <FiscalAttestationsPage />
    </NextIntlClientProvider>,
  );
}

function makeAttestation(overrides: Partial<FiscalAttestationResponse> = {}): FiscalAttestationResponse {
  return {
    id: "att-1",
    childId: "child-1",
    childName: "Emma Peeters",
    locationId: "loc-1",
    locationName: "Sunshine House",
    taxYear: 2026,
    totalAmountCents: 84000,
    status: "generated",
    periods: [{ periodStart: "2027-01-01", periodEnd: "2027-12-31", days: 24, amountCents: 84000, dailyRateCents: 3500 }],
    generatedAt: "2027-01-15T10:00:00Z",
    ...overrides,
  };
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.fiscalAttestation.generic" } };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("FiscalAttestationsPage", () => {
  it("shows an empty state when nothing has been generated yet for the selected tax year", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    renderPage();

    expect(await screen.findByText("No attestations generated yet for this tax year.")).toBeInTheDocument();
  });

  it("renders a distinct row status for generated, not-yet-generated, and failed", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(
      okResponse([
        makeAttestation(),
        makeAttestation({ id: null, childId: "child-2", childName: "Lucas Peeters", status: "notYetGenerated", totalAmountCents: null, periods: null, generatedAt: null }),
      ]) as never,
    );
    renderPage();

    const table = await screen.findByRole("table");
    const rows = within(table).getAllByRole("row");
    const emmaRow = rows.find((r) => within(r).queryByText("Emma Peeters"))!;
    const lucasRow = rows.find((r) => within(r).queryByText("Lucas Peeters"))!;
    expect(within(emmaRow).getByText("Generated")).toBeInTheDocument();
    expect(within(lucasRow).getByText("No paid invoices this year")).toBeInTheDocument();
    // A not-yet-generated row has no download action (no PDF exists), only regenerate.
    expect(within(lucasRow).queryByRole("button", { name: "Download PDF" })).not.toBeInTheDocument();
    expect(within(lucasRow).getByRole("button", { name: "Regenerate" })).toBeInTheDocument();
  });

  it("triggers bulk generation and reloads the list", async () => {
    vi.mocked(apiClient.GET).mockResolvedValueOnce(okResponse([]) as never).mockResolvedValue(okResponse([makeAttestation()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({ taxYear: 2026, results: [{ childId: "child-1", locationId: "loc-1", status: "generated" }] }) as never);
    renderPage();

    await screen.findByText("No attestations generated yet for this tax year.");
    await userEvent.click(screen.getByRole("button", { name: "Generate attestations" }));

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(apiClient.POST).toHaveBeenCalledWith("/api/fiscal-attestations/generate", { body: { taxYear: 2026 } });
  });

  it("regenerating one row updates only that row's status via a full reload", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([makeAttestation()]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeAttestation({ totalAmountCents: 90000 })) as never);
    renderPage();

    await screen.findByText("Emma Peeters");
    await userEvent.click(screen.getByRole("button", { name: "Regenerate" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/fiscal-attestations/{childId}/{locationId}/{taxYear}/regenerate",
      expect.objectContaining({ params: { path: { childId: "child-1", locationId: "loc-1", taxYear: 2026 } } }),
    );
    expect(await screen.findByText("Attestation regenerated.")).toBeInTheDocument();
  });

  it("shows an error state when loading attestations fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(errorResponse(500) as never);
    renderPage();

    expect(await screen.findByText("Couldn't load attestations. Please try again.")).toBeInTheDocument();
  });
});
