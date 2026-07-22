import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import ContractsPage from "../app/(app)/contracts/page";
import { apiClient } from "../lib/apiClient";
import type { ContractSummaryResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ContractsPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number, errorKey: string) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey } };
}

function makeContract(overrides: Partial<ContractSummaryResponse> = {}): ContractSummaryResponse {
  return {
    id: "contract-1",
    childId: "child-1",
    childName: "Emma Peeters",
    locationName: "Main Building",
    startDate: "2026-01-01",
    dailyRateCents: 3500,
    status: "draft",
    signingStatus: "notsent",
    signedAt: null,
    ...overrides,
  };
}

function mockGet(byPath: Record<string, unknown>) {
  vi.mocked(apiClient.GET).mockImplementation(((path: unknown) => {
    if (typeof path === "string" && path in byPath) return Promise.resolve(okResponse(byPath[path]));
    return Promise.resolve(okResponse([]));
  }) as never);
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("ContractsPage", () => {
  it("shows the empty state when there are no contracts", async () => {
    mockGet({ "/api/contracts": [] });
    renderPage();

    expect(await screen.findByText("No contracts yet.")).toBeInTheDocument();
  });

  it("renders a Draft contract with a Send for signature action", async () => {
    mockGet({ "/api/contracts": [makeContract()] });
    renderPage();

    const table = await screen.findByRole("table");
    expect(within(table).getByText("Emma Peeters")).toBeInTheDocument();
    expect(within(table).getByText("Main Building")).toBeInTheDocument();
    expect(within(table).getByText("Not sent")).toBeInTheDocument();
    expect(within(table).getByRole("button", { name: "Send for signature" })).toBeInTheDocument();
  });

  it("shows a Resend action for a pending invitation, and a Signed badge with date once signed", async () => {
    mockGet({
      "/api/contracts": [
        makeContract({ id: "contract-pending", signingStatus: "pending" }),
        makeContract({ id: "contract-signed", signingStatus: "signed", signedAt: "2026-07-10T00:00:00Z" }),
      ],
    });
    renderPage();

    const table = await screen.findByRole("table");
    expect(within(table).getByRole("button", { name: "Resend" })).toBeInTheDocument();
    expect(within(table).getByText("Signed")).toBeInTheDocument();
    expect(within(table).getByRole("button", { name: "View signed PDF" })).toBeInTheDocument();
  });

  it("sending an invitation calls the API and shows a success notice", async () => {
    mockGet({ "/api/contracts": [makeContract()] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({ signed: false }) as never);
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "Send for signature" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/contracts/{id}/signing-invitation",
      expect.objectContaining({ params: { path: { id: "contract-1" } } }),
    );
    expect(await screen.findByText("Signing invitation sent.")).toBeInTheDocument();
  });

  it("shows a specific message when the creditor ID isn't configured yet", async () => {
    mockGet({ "/api/contracts": [makeContract()] });
    vi.mocked(apiClient.POST).mockResolvedValue(
      errorResponse(422, "errors.contract_signing.creditor_id_not_configured") as never,
    );
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "Send for signature" }));

    expect(
      await screen.findByText("Set your organisation's SEPA Creditor Identifier in settings before sending a signing invitation."),
    ).toBeInTheDocument();
  });

  it("viewing the signed PDF opens the returned download URL", async () => {
    mockGet({
      "/api/contracts": [makeContract({ signingStatus: "signed", signedAt: "2026-07-10T00:00:00Z" })],
      "/api/contracts/{id}/signed-pdf-url": { downloadUrl: "https://storage.test/signed-contracts/contract-1.pdf", expiresAt: "2026-07-10T01:00:00Z" },
    });
    const openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "View signed PDF" }));

    expect(openSpy).toHaveBeenCalledWith("https://storage.test/signed-contracts/contract-1.pdf", "_blank", "noopener,noreferrer");
    openSpy.mockRestore();
  });
});
