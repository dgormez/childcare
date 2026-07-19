import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import AnnouncementsPage from "../app/(app)/announcements/page";
import { apiClient } from "../lib/apiClient";
import type { AnnouncementResponse, GroupResponse, LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderAnnouncementsPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <AnnouncementsPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
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

function makeAnnouncement(overrides: Partial<AnnouncementResponse> = {}): AnnouncementResponse {
  return {
    id: "announce-1",
    locationId: "loc-1",
    groupId: null,
    subject: "Closed Friday",
    body: "Staff training day",
    sentByTenantUserId: "director-1",
    sentAt: "2026-07-10T09:00:00Z",
    recipientCount: 4,
    ...overrides,
  };
}

function mockGet(byPath: Record<string, unknown>) {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    if (typeof path === "string" && path in byPath) return Promise.resolve(okResponse(byPath[path])) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("AnnouncementsPage", () => {
  it("loads the sent-history list", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/groups": [] as GroupResponse[],
      "/api/announcements": [makeAnnouncement()],
    });

    renderAnnouncementsPage();

    expect(await screen.findByText("Closed Friday")).toBeInTheDocument();
    expect(screen.getByText(/Sunshine House/)).toBeInTheDocument();
  });

  it("renders the empty state with no announcements sent yet", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/groups": [] as GroupResponse[],
      "/api/announcements": [] as AnnouncementResponse[],
    });

    renderAnnouncementsPage();

    expect(await screen.findByText("No announcements sent yet.")).toBeInTheDocument();
  });

  it("composes and sends a location-scoped announcement", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/groups": [] as GroupResponse[],
      "/api/announcements": [] as AnnouncementResponse[],
    });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeAnnouncement()) as never);

    renderAnnouncementsPage();
    await screen.findByText("No announcements sent yet.");

    await userEvent.click(screen.getByRole("button", { name: "New announcement" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Subject"), "Closed Friday");
    await userEvent.type(screen.getByLabelText("Message"), "Staff training day");
    await userEvent.click(within(dialog).getByRole("button", { name: "Send announcement" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/announcements",
      expect.objectContaining({ body: expect.objectContaining({ locationId: "loc-1", subject: "Closed Friday", body: "Staff training day" }) }),
    );
  });
});
