import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import OrganisationSettingsPage from "../app/(app)/settings/page";
import { apiClient } from "../lib/apiClient";
import type { OrganisationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PUT: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <OrganisationSettingsPage />
    </NextIntlClientProvider>,
  );
}

const organisation: OrganisationResponse = { name: "Sunshine Kinderopvang", kboNumber: "0123.456.789" };

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.organisation.not_found" } };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
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
