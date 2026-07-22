import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import PublicContractSigningPage from "../app/sign/page";
import { publicApiClient } from "../lib/publicApiClient";
import type { ContractForSigningResponse } from "../lib/types";

let searchParams = new URLSearchParams({ org: "sunshine", token: "valid-token" });
vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams,
}));

vi.mock("../lib/publicApiClient", () => ({
  publicApiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number, errorKey?: string) {
  return { response: new Response(null, { status }), data: undefined, error: errorKey ? { errorKey } : undefined };
}

const contract: ContractForSigningResponse = {
  childName: "Emma Peeters",
  locationName: "Main Building",
  contractedDays: [{ weekday: "Monday", startTime: "08:00", endTime: "17:00" }],
  dailyRateCents: 3500,
  consent: { photosInternal: true, photosWebsite: false, photosSocialMedia: false, videoInternal: false, photosPress: false },
  locale: "en",
};

beforeEach(() => {
  searchParams = new URLSearchParams({ org: "sunshine", token: "valid-token" });
  vi.mocked(publicApiClient.GET).mockReset();
  vi.mocked(publicApiClient.POST).mockReset();
});

describe("PublicContractSigningPage", () => {
  it("shows the invalid-link message when the token is missing", async () => {
    searchParams = new URLSearchParams();
    render(<PublicContractSigningPage />);

    expect(await screen.findByText("Deze link is niet langer geldig. Neem contact op met de kinderopvang voor een nieuwe link.")).toBeInTheDocument();
  });

  it("shows the invalid-link message (defaulting to Dutch), with a working language toggle", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(errorResponse(404, "errors.contract_signing.invalid_or_expired") as never);
    render(<PublicContractSigningPage />);

    expect(await screen.findByText("Deze link is niet langer geldig. Neem contact op met de kinderopvang voor een nieuwe link.")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "English" }));
    expect(await screen.findByText("This link is no longer valid. Please contact the childcare organisation for a new one.")).toBeInTheDocument();
  });

  it("renders the contract terms before any signature input", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(okResponse(contract) as never);
    render(<PublicContractSigningPage />);

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("Main Building")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign contract" })).toBeDisabled();
  });

  it("submits with a typed signature, IBAN, and both confirmations, and shows the confirmation screen", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(okResponse(contract) as never);
    vi.mocked(publicApiClient.POST).mockResolvedValue(okResponse({ signed: true }) as never);
    render(<PublicContractSigningPage />);

    await screen.findByText("Emma Peeters");
    await userEvent.click(screen.getByRole("tab", { name: "Type" }));
    await userEvent.type(screen.getByPlaceholderText("Type your full name"), "Emma Peeters");
    await userEvent.click(screen.getByText("I have read and reviewed the contract terms above and I intend to sign this contract."));
    await userEvent.type(screen.getByLabelText("IBAN"), "BE68539007547034");
    await userEvent.click(screen.getByText(/I authorise the organisation to collect payments/));

    const submit = screen.getByRole("button", { name: "Sign contract" });
    expect(submit).toBeEnabled();
    await userEvent.click(submit);

    expect(publicApiClient.POST).toHaveBeenCalledWith("/api/public/contracts/sign", {
      params: { query: { org: "sunshine", token: "valid-token" } },
      body: { signatureType: "Typed", signatureData: "Emma Peeters", sepaIban: "BE68539007547034" },
    });
    expect(await screen.findByText("Contract signed")).toBeInTheDocument();
  });

  it("shows an inline error for an invalid IBAN without losing the entered signature", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(okResponse(contract) as never);
    vi.mocked(publicApiClient.POST).mockResolvedValue(
      errorResponse(422, "errors.contract_signing.invalid_iban") as never,
    );
    render(<PublicContractSigningPage />);

    await screen.findByText("Emma Peeters");
    await userEvent.click(screen.getByRole("tab", { name: "Type" }));
    await userEvent.type(screen.getByPlaceholderText("Type your full name"), "Emma Peeters");
    await userEvent.click(screen.getByText("I have read and reviewed the contract terms above and I intend to sign this contract."));
    await userEvent.type(screen.getByLabelText("IBAN"), "BE68539007547035");
    await userEvent.click(screen.getByText(/I authorise the organisation to collect payments/));
    await userEvent.click(screen.getByRole("button", { name: "Sign contract" }));

    expect(await screen.findByText("This doesn't look like a valid IBAN. Please check and try again.")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Type your full name")).toHaveValue("Emma Peeters");
  });
});
