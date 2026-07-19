import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import ClosuresPage from "../app/(app)/closures/page";
import { apiClient } from "../lib/apiClient";
import type { ClosureDayResponse, LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PATCH: vi.fn() },
}));

function renderClosuresPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ClosuresPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number, errorKey: string) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey } };
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
    deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function makeClosure(overrides: Partial<ClosureDayResponse> = {}): ClosureDayResponse {
  return {
    id: "closure-1",
    locationId: "loc-1",
    date: "2026-07-13",
    label: "Kerstvakantie",
    closureType: "holiday",
    notifyParents: true,
    status: "draft",
    notificationSentAt: null,
    publishedAt: null,
    cancelledAt: null,
    deliverySummary: { sent: 0, failed: 0, messageCount: 0 },
    createdAt: "2026-07-09T12:00:00Z",
    updatedAt: "2026-07-09T12:00:00Z",
    ...overrides,
  };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.PATCH).mockReset();
});

describe("ClosuresPage", () => {
  it("renders a location-year closure calendar and side list", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/closures") return Promise.resolve(okResponse([makeClosure({ status: "published" })])) as never;
      return Promise.resolve(okResponse([])) as never;
    });

    renderClosuresPage();

    expect(await screen.findByText("Kerstvakantie")).toBeInTheDocument();
    expect(screen.getByText("Published")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "13: Kerstvakantie, Holiday" })).toBeInTheDocument();
  });

  it("shows an empty state when the location has no closure days", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/closures") return Promise.resolve(okResponse([])) as never;
      return Promise.resolve(okResponse([])) as never;
    });

    renderClosuresPage();

    expect(await screen.findByText("No closure days for this location and year yet.")).toBeInTheDocument();
  });

  it("keeps entered values visible when create fails", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/closures") return Promise.resolve(okResponse([])) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse(409, "errors.closures.duplicate_date") as never);

    renderClosuresPage();
    await screen.findByText("No closure days for this location and year yet.");

    await userEvent.click(screen.getByRole("button", { name: "Add closure" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.clear(within(dialog).getByLabelText("Label"));
    await userEvent.type(within(dialog).getByLabelText("Label"), "Training");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await screen.findByText("This location already has a closure on that date.")).toBeInTheDocument();
    expect(within(dialog).getByDisplayValue("Training")).toBeInTheDocument();
  });

  it("retries publish with attendance confirmation after a conflict", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/closures") return Promise.resolve(okResponse([makeClosure()])) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.POST)
      .mockResolvedValueOnce(errorResponse(409, "errors.closures.attendance_confirmation_required") as never)
      .mockResolvedValueOnce(okResponse({
        closure: makeClosure({ status: "published" }),
        attendanceRecordsCreated: 0,
        attendanceRecordsUpdated: 1,
        requiresAttendanceConfirmation: false,
        notificationSummary: { recipients: 0, pushSent: 0, pushFailed: 0, messagesCreated: 0 },
      }) as never);

    renderClosuresPage();
    await screen.findByText("Kerstvakantie");

    await userEvent.click(screen.getByRole("button", { name: "Publish" }));
    await userEvent.click(await screen.findByRole("button", { name: "Publish" }));
    expect(await screen.findByText("Some children are already checked in for this date.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Publish anyway" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledTimes(2));
  });

  it("does not show attendance confirmation for other publish conflicts", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/closures") return Promise.resolve(okResponse([makeClosure()])) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse(409, "errors.closures.not_publishable") as never);

    renderClosuresPage();
    await screen.findByText("Kerstvakantie");

    await userEvent.click(screen.getByRole("button", { name: "Publish" }));
    await userEvent.click(await screen.findByRole("button", { name: "Publish" }));

    expect(await screen.findByText("Couldn't save this closure. Please try again.")).toBeInTheDocument();
    expect(screen.queryByText("Some children are already checked in for this date.")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Publish anyway" })).not.toBeInTheDocument();
    expect(apiClient.POST).toHaveBeenCalledTimes(1);
  });

  it("cancels a published closure after confirmation", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/closures") return Promise.resolve(okResponse([makeClosure({ status: "published" })])) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({
      closure: makeClosure({ status: "cancelled" }),
      attendanceRecordsReleased: 1,
      attendanceRecordsPreserved: 0,
      notificationSummary: { recipients: 1, pushSent: 1, pushFailed: 0, messagesCreated: 1 },
    }) as never);

    renderClosuresPage();
    await screen.findByText("Published");

    await userEvent.click(screen.getByRole("button", { name: "Cancel closure" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Cancel closure" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/closures/{id}/cancel",
      expect.objectContaining({ params: { path: { id: "closure-1" } } }),
    ));
    expect(await screen.findByText("1 cancellation messages created, 1 attendance records released.")).toBeInTheDocument();
  });
});
