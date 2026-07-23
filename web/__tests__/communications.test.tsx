import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import CommunicationsPage from "../app/(app)/communications/page";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <CommunicationsPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
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
  publicEnrollmentEnabled: false,
  publicEnrollmentSlug: "loc-1",
  defaultEnrollmentLocale: "nl",
  deactivatedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

function mockGetForSend() {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    const key = String(path);
    if (key === "/api/locations") return Promise.resolve(okResponse([location])) as never;
    if (key === "/api/groups") return Promise.resolve(okResponse([])) as never;
    if (key === "/api/email/bulk-send/recipient-count") return Promise.resolve(okResponse({ recipientCount: 3 })) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

describe("CommunicationsPage", () => {
  it("sends comma-separated Cc/Bcc addresses as parsed arrays", async () => {
    mockGetForSend();
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({ sentCount: 3, skippedNoEmailCount: 0, providerFailureCount: 0 }) as never);

    renderPage();
    await screen.findByText("3 families will receive this email");

    await userEvent.type(screen.getByLabelText("Subject"), "Reminder");
    await userEvent.type(screen.getByLabelText("Message"), "Please bring boots tomorrow.");
    await userEvent.type(screen.getByLabelText("Cc (optional)"), "co-director@test.com, other@test.com");
    await userEvent.type(screen.getByLabelText("Bcc (optional)"), "archive@test.com");
    await userEvent.click(screen.getByRole("button", { name: "Send email" }));

    expect(await screen.findByText("3 sent")).toBeInTheDocument();
    expect(apiClient.POST).toHaveBeenCalledWith("/api/email/bulk-send", {
      body: expect.objectContaining({
        cc: ["co-director@test.com", "other@test.com"],
        bcc: ["archive@test.com"],
      }),
    });
  });

  it("rejects an invalid Cc address client-side, without sending", async () => {
    mockGetForSend();

    renderPage();
    await screen.findByText("3 families will receive this email");

    await userEvent.type(screen.getByLabelText("Subject"), "Reminder");
    await userEvent.type(screen.getByLabelText("Message"), "Please bring boots tomorrow.");
    await userEvent.type(screen.getByLabelText("Cc (optional)"), "not-an-email");
    await userEvent.click(screen.getByRole("button", { name: "Send email" }));

    expect(await screen.findByText("Enter valid email addresses, separated by commas.")).toBeInTheDocument();
    expect(apiClient.POST).not.toHaveBeenCalled();
  });
});
