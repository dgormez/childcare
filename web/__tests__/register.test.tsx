import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RegisterPage from "../app/register/page";
import { publicApiClient } from "../lib/publicApiClient";
import type { InvitationInfoResponse } from "../lib/types";

let searchParams = new URLSearchParams({ token: "valid-token" });
vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams,
}));

vi.mock("../lib/publicApiClient", () => ({
  publicApiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: undefined };
}

const invitationInfo: InvitationInfoResponse = { email: "prospective@test.com" };

beforeEach(() => {
  searchParams = new URLSearchParams({ token: "valid-token" });
  vi.mocked(publicApiClient.GET).mockReset();
  vi.mocked(publicApiClient.POST).mockReset();
});

// Tasks.md T018/T019 — User Story 2's component tests: pre-filled/locked email for a valid
// token, a submit success state, and one generic "no longer valid" message for expired/revoked/
// already-used/never-existed tokens (FR-011 — never a reason-specific message).
describe("RegisterPage", () => {
  it("shows the invalid-link message when the token is missing", async () => {
    searchParams = new URLSearchParams();
    render(<RegisterPage />);

    expect(await screen.findByText(/Deze uitnodigingslink is niet meer geldig/)).toBeInTheDocument();
  });

  it("shows the same generic invalid-link message for a 404 lookup response", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(errorResponse(404) as never);
    render(<RegisterPage />);

    expect(await screen.findByText(/Deze uitnodigingslink is niet meer geldig/)).toBeInTheDocument();
  });

  it("pre-fills and locks the email for a valid token", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(okResponse(invitationInfo) as never);
    render(<RegisterPage />);

    const emailInput = await screen.findByDisplayValue("prospective@test.com");
    expect(emailInput).toBeDisabled();
  });

  it("submits the form and shows the confirmation screen", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(okResponse(invitationInfo) as never);
    vi.mocked(publicApiClient.POST).mockResolvedValue(okResponse({ accessToken: "t" }) as never);
    render(<RegisterPage />);

    await screen.findByDisplayValue("prospective@test.com");
    await userEvent.type(screen.getByLabelText("Naam organisatie"), "Zonnebloem KDV");
    await userEvent.type(screen.getByLabelText("Uw naam"), "Jane Director");
    await userEvent.type(screen.getByLabelText("Wachtwoord"), "password123");
    await userEvent.click(screen.getByRole("button", { name: "Account aanmaken" }));

    expect(publicApiClient.POST).toHaveBeenCalledWith("/api/organisations/register", {
      body: {
        invitationToken: "valid-token",
        organisationName: "Zonnebloem KDV",
        directorName: "Jane Director",
        email: "prospective@test.com",
        password: "password123",
      },
    });
    expect(await screen.findByText("Uw organisatie is aangemaakt. U kunt nu inloggen met de e-mail en het wachtwoord die u zojuist heeft ingesteld.")).toBeInTheDocument();
  });

  it("shows the invalid-link message if the token becomes invalid by the time of submission", async () => {
    vi.mocked(publicApiClient.GET).mockResolvedValue(okResponse(invitationInfo) as never);
    vi.mocked(publicApiClient.POST).mockResolvedValue(errorResponse(404) as never);
    render(<RegisterPage />);

    await screen.findByDisplayValue("prospective@test.com");
    await userEvent.type(screen.getByLabelText("Naam organisatie"), "Zonnebloem KDV");
    await userEvent.type(screen.getByLabelText("Uw naam"), "Jane Director");
    await userEvent.type(screen.getByLabelText("Wachtwoord"), "password123");
    await userEvent.click(screen.getByRole("button", { name: "Account aanmaken" }));

    expect(await screen.findByText(/Deze uitnodigingslink is niet meer geldig/)).toBeInTheDocument();
  });
});
