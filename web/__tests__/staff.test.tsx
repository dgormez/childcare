import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import StaffPage from "../app/(app)/staff/page";
import { apiClient } from "../lib/apiClient";
import type { StaffResponse, LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PUT: vi.fn(), POST: vi.fn() },
}));

function renderStaffPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <StaffPage />
    </NextIntlClientProvider>,
  );
}

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
  publicEnrollmentEnabled: false, publicEnrollmentSlug: "loc-1", defaultEnrollmentLocale: "nl",
  deactivatedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function makeStaff(overrides: Partial<StaffResponse> = {}): StaffResponse {
  return {
    id: "staff-1",
    tenantUserId: "user-1",
    firstName: "Marie",
    lastName: "Janssens",
    email: "marie@test.com",
    phone: "+32 9 111 11 11",
    role: "Staff",
    qualificationLevel: "QualifiedCaregiver",
    photoDownloadUrl: null,
    eligibleLocationIds: ["loc-1"],
    deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function okResponse(data: unknown): { response: Response; data: unknown; error: undefined } {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number, errorKey: string) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey } };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PUT).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("StaffPage", () => {
  it("renders staff rows with resolved location names", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: string) => {
      if (path === "/api/staff") return Promise.resolve(okResponse([makeStaff()])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
    });

    renderStaffPage();

    expect(await screen.findByText("Marie Janssens")).toBeInTheDocument();
    expect(screen.getByText("Sunshine House")).toBeInTheDocument();
  });

  it("filters the table by search input", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: string) => {
      if (path === "/api/staff") {
        return Promise.resolve(
          okResponse([makeStaff({ id: "s1", firstName: "Marie" }), makeStaff({ id: "s2", firstName: "Thomas" })]),
        ) as ReturnType<typeof apiClient.GET>;
      }
      return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
    });

    renderStaffPage();
    await screen.findByText("Marie Janssens");
    expect(screen.getByText("Thomas Janssens")).toBeInTheDocument();

    await userEvent.type(screen.getByPlaceholderText("Search by name…"), "Thomas");

    expect(screen.queryByText("Marie Janssens")).not.toBeInTheDocument();
    expect(screen.getByText("Thomas Janssens")).toBeInTheDocument();
  });

  it("shows an inline error when a PIN reset conflicts with another caregiver's PIN", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: string) => {
      if (path === "/api/staff") return Promise.resolve(okResponse([makeStaff()])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
    });
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse(409, "errors.pin.not_unique_at_location") as never);

    renderStaffPage();
    await screen.findByText("Marie Janssens");

    await userEvent.click(screen.getByRole("button", { name: "Reset PIN" }));
    await userEvent.type(screen.getByLabelText("4-digit PIN"), "1234");
    await userEvent.click(screen.getByRole("button", { name: "Save PIN" }));

    expect(await screen.findByText("This PIN is already used by another caregiver at the same location.")).toBeInTheDocument();
  });

  it("deactivates a staff member after confirmation", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: string) => {
      if (path === "/api/staff") return Promise.resolve(okResponse([makeStaff()])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
    });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({}) as never);

    renderStaffPage();
    await screen.findByText("Marie Janssens");

    await userEvent.click(screen.getByRole("button", { name: "Deactivate" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Deactivate" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/staff/{id}/deactivate",
      expect.objectContaining({ params: { path: { id: "staff-1" } } }),
    ));
  });

  it("shows an empty state when the tenant has no staff", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: string) => {
      if (path === "/api/staff") return Promise.resolve(okResponse([])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([])) as ReturnType<typeof apiClient.GET>;
    });

    renderStaffPage();

    expect(await screen.findByText("No staff members yet.")).toBeInTheDocument();
  });

  it("shows a retryable error state when the staff list fails to load", async () => {
    vi.mocked(apiClient.GET).mockResolvedValue({ response: new Response(null, { status: 500 }), data: undefined, error: {} } as never);

    renderStaffPage();

    expect(await screen.findByText("Couldn't load staff. Please try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });
});
