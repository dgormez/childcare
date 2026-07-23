import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import LocationsPage from "../app/(app)/locations/page";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse } from "../lib/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderComponent() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <LocationsPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: {} };
}

function makeLocation(overrides: Partial<LocationResponse> = {}): LocationResponse {
  return {
    id: "loc-1",
    name: "Sunshine House",
    address: "1 Main St",
    phone: "+32 9 123 45 67",
    email: "loc@test.com",
    maxCapacity: 20,
    naamLocatie: null,
    dossiernummer: null,
    verantwoordelijke: null,
    flexPermission: false,
    boPermission: false,
    reservationAbsencesMode: "approval",
    reservationExtrasMode: "approval",
    reservationSwapsMode: "disabled",
    reservationNoticeHours: 0,
    requiresCaregiverPin: true,
    menuVariantPriorityOrder: [],
    menuVariantsWithPublishedContent: [],
    erkenningsnummer: null,
    bankAccountNumber: null,
    invoiceDueDays: 14,
    paymentRemindersEnabled: false,
    paymentReminderDelayDays: 3,
    paymentReminderCadenceDays: 7,
    siblingDiscountPct: 0,
    familyInvoiceBundlingEnabled: false,
    qrCheckInEnabled: false,
    publicEnrollmentEnabled: false,
    publicEnrollmentSlug: "loc-1",
    defaultEnrollmentLocale: "nl",
    deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  push.mockReset();
});

describe("LocationsPage", () => {
  it("creates a location and navigates to its detail page", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeLocation({ id: "loc-new" })) as never);

    renderComponent();
    await screen.findByText("No locations yet.");

    await userEvent.click(screen.getByRole("button", { name: "Add location" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Name"), "Sunshine House");
    await userEvent.type(within(dialog).getByLabelText("Address"), "1 Main St");
    await userEvent.type(within(dialog).getByLabelText("Phone"), "+32 9 123 45 67");
    await userEvent.type(within(dialog).getByLabelText("Email"), "loc@test.com");
    await userEvent.click(within(dialog).getByRole("button", { name: "Create" }));

    expect(apiClient.POST).toHaveBeenCalledWith("/api/locations", {
      body: { name: "Sunshine House", address: "1 Main St", phone: "+32 9 123 45 67", email: "loc@test.com", maxCapacity: 1 },
    });
    expect(push).toHaveBeenCalledWith("/locations/loc-new");
  });

  it("shows an error and keeps entered values when creation fails", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue(okResponse([]) as never);
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse(500) as never);

    renderComponent();
    await screen.findByText("No locations yet.");

    await userEvent.click(screen.getByRole("button", { name: "Add location" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Name"), "Sunshine House");
    await userEvent.type(within(dialog).getByLabelText("Address"), "1 Main St");
    await userEvent.type(within(dialog).getByLabelText("Phone"), "+32 9 123 45 67");
    await userEvent.type(within(dialog).getByLabelText("Email"), "loc@test.com");
    await userEvent.click(within(dialog).getByRole("button", { name: "Create" }));

    expect(await screen.findByText("Couldn't create this location. Please try again.")).toBeInTheDocument();
    expect(within(dialog).getByDisplayValue("Sunshine House")).toBeInTheDocument();
  });
});
