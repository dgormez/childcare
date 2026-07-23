import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import MonthlyMenuPage from "../app/(app)/menu/page";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse, MonthlyMenuResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PUT: vi.fn(), POST: vi.fn() },
}));

const location: LocationResponse = {
  id: "loc-1",
  name: "Sunshine House",
  address: "1 Main St",
  phone: "+32 9 000 00 00",
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
  menuVariantPriorityOrder: ["halal"],
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
  publicEnrollmentEnabled: false, publicEnrollmentSlug: "loc-1", defaultEnrollmentLocale: "nl",
  deactivatedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const now = new Date();
const firstOfMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;

const baseMenu: MonthlyMenuResponse = {
  exists: true,
  isPublished: false,
  publishedAt: null,
  variant: null,
  days: [{ date: firstOfMonth, lunchMeal: "Tomatensoep", alternativeLunchMeal: "Kip met puree", snack: "Yoghurt", notes: null }],
};

const halalMenu: MonthlyMenuResponse = {
  exists: true,
  isPublished: false,
  publishedAt: null,
  variant: "halal",
  days: [{ date: firstOfMonth, lunchMeal: "Groentesoep", alternativeLunchMeal: "Rund met rijst", snack: "Fruit", notes: null }],
};

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
  vi.mocked(apiClient.POST).mockReset();

  vi.mocked(apiClient.GET).mockImplementation((async (path: string, options?: { params?: { query?: { variant?: string } } }) => {
    if (path === "/api/locations") return okResponse([location]);
    if (path === "/api/meal-preference-requests") return okResponse([]);
    if (path === "/api/locations/{locationId}/monthly-menus/{year}/{month}") {
      const variant = options?.params?.query?.variant;
      return okResponse(variant === "halal" ? halalMenu : baseMenu);
    }
    throw new Error(`Unhandled GET ${path}`);
  }) as never);
});

function renderMenuPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <MonthlyMenuPage />
    </NextIntlClientProvider>,
  );
}

describe("MonthlyMenuPage variant switching", () => {
  it("loads the base menu by default and switches to the variant's distinct menu when selected", async () => {
    renderMenuPage();

    expect(await screen.findByDisplayValue("Kip met puree")).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText("Menu"), "halal");

    expect(await screen.findByDisplayValue("Rund met rijst")).toBeInTheDocument();
    expect(screen.queryByDisplayValue("Kip met puree")).not.toBeInTheDocument();

    expect(apiClient.GET).toHaveBeenLastCalledWith(
      "/api/locations/{locationId}/monthly-menus/{year}/{month}",
      expect.objectContaining({
        params: expect.objectContaining({ query: { variant: "halal" } }),
      }),
    );
  });
});
