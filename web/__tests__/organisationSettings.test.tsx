import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import OrganisationSettingsPage from "../app/(app)/settings/page";
import { apiClient } from "../lib/apiClient";
import type { OrganisationResponse, PaymentConnectionResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PUT: vi.fn(), POST: vi.fn(), DELETE: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <OrganisationSettingsPage />
    </NextIntlClientProvider>,
  );
}

const organisation: OrganisationResponse = { name: "Sunshine Kinderopvang", kboNumber: "0123.456.789" };
const disconnected: PaymentConnectionResponse = { status: "disconnected", providerAccountLabel: null, connectedAt: null };
const connected: PaymentConnectionResponse = { status: "connected", providerAccountLabel: "Sunshine Kinderopvang (Mollie)", connectedAt: "2026-07-16T00:00:00Z" };

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.organisation.not_found" } };
}

// Feature 014a — the page now fires two independent GET calls (org profile, payment
// connection); mock by URL rather than a single blanket resolved value.
function mockGetByUrl(connectionResponse: ReturnType<typeof okResponse> | ReturnType<typeof errorResponse> = okResponse(disconnected)) {
  vi.mocked(apiClient.GET).mockImplementation((async (url: string) => {
    if (url === "/api/organisations/me/payment-connection") return connectionResponse;
    return okResponse(organisation);
  }) as never);
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.DELETE).mockReset();
  mockGetByUrl();
});

describe("OrganisationSettingsPage", () => {
  it("loads and displays the current KBO number", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse(organisation) as never);
    renderPage();

    expect(await screen.findByLabelText("KBO / ondernemingsnummer")).toHaveValue("0123.456.789");
  });

  it("shows an error state when loading fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(errorResponse(500) as never);
    renderPage();

    expect(await screen.findByText("Couldn't load organisation settings. Please try again.")).toBeInTheDocument();
  });

  it("saves the KBO number and shows a success notice", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse(organisation) as never);
    const updated = { ...organisation, kboNumber: "0987.654.321" };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    renderPage();

    const input = await screen.findByLabelText("KBO / ondernemingsnummer");
    await userEvent.clear(input);
    await userEvent.type(input, "0987.654.321");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() =>
      expect(apiClient.PUT).toHaveBeenCalledWith("/api/organisations/me", { body: { kboNumber: "0987.654.321" } }),
    );
    expect(await screen.findByText("Organisation settings saved.")).toBeInTheDocument();
  });
});

describe("OrganisationSettingsPage — payment connection (feature 014a)", () => {
  it("shows a not-connected state with a Connect Mollie action by default", async () => {
    renderPage();

    expect(await screen.findByRole("button", { name: "Connect Mollie" })).toBeInTheDocument();
    expect(screen.queryByText("Connected")).not.toBeInTheDocument();
  });

  it("redirects to the returned authorization URL when Connect Mollie is clicked", async () => {
    vi.mocked(apiClient.POST).mockResolvedValue(
      okResponse({ authorizationUrl: "https://www.mollie.com/oauth2/authorize?state=abc" }) as never,
    );
    const originalLocation = window.location;
    // @ts-expect-error — replacing window.location for this test only
    delete window.location;
    window.location = { ...originalLocation, href: "" } as Location;

    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "Connect Mollie" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith("/api/organisations/me/payment-connection/authorize"));
    expect(window.location.href).toBe("https://www.mollie.com/oauth2/authorize?state=abc");
    window.location = originalLocation;
  });

  it("shows a connected state with the account label and a disconnect action", async () => {
    mockGetByUrl(okResponse(connected));
    renderPage();

    expect(await screen.findByText("Connected")).toBeInTheDocument();
    expect(screen.getByText("Sunshine Kinderopvang (Mollie)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Disconnect" })).toBeInTheDocument();
  });

  it("disconnecting reverts to the not-connected state", async () => {
    let stillConnected = true;
    vi.mocked(apiClient.GET).mockImplementation((async (url: string) => {
      if (url === "/api/organisations/me/payment-connection") return okResponse(stillConnected ? connected : disconnected);
      return okResponse(organisation);
    }) as never);
    vi.mocked(apiClient.DELETE).mockImplementation((async () => {
      stillConnected = false;
      return { response: new Response(null, { status: 204 }), data: undefined, error: undefined };
    }) as never);
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "Disconnect" }));

    await waitFor(() => expect(apiClient.DELETE).toHaveBeenCalledWith("/api/organisations/me/payment-connection"));
    expect(await screen.findByRole("button", { name: "Connect Mollie" })).toBeInTheDocument();
  });
});
