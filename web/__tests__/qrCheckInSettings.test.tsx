import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { QrCheckInSettingsForm } from "../components/QrCheckInSettingsForm";
import { apiClient } from "../lib/apiClient";
import type { LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { PUT: vi.fn() },
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

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.location.not_found" } };
}

function renderForm(onSaved = vi.fn()) {
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <QrCheckInSettingsForm location={location} onSaved={onSaved} />
    </NextIntlClientProvider>,
  );
  return onSaved;
}

beforeEach(() => {
  vi.mocked(apiClient.PUT).mockReset();
});

describe("QrCheckInSettingsForm", () => {
  it("loads the current toggle state and shows the explanatory copy alongside it (FR-003)", () => {
    renderForm();
    expect(screen.getByLabelText("Allow QR check-in")).not.toBeChecked();
    expect(screen.getByText(/parents can show a code in the parent app/)).toBeInTheDocument();
    expect(screen.getByText(/handover itself doesn't change/)).toBeInTheDocument();
  });

  it("saves a toggle change and reflects the updated value via onSaved", async () => {
    const updated = { ...location, qrCheckInEnabled: true };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.click(screen.getByLabelText("Allow QR check-in"));
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/qr-checkin-setting",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: { enabled: true },
      }),
    );
  });

  it("reverts the toggle and shows an error notice on a failed save (FR-018)", async () => {
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse(404) as never);
    const onSaved = renderForm();

    const toggle = screen.getByLabelText("Allow QR check-in");
    await userEvent.click(toggle);
    expect(toggle).toBeChecked();

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Couldn't save changes. Please try again.")).toBeInTheDocument();
    expect(toggle).not.toBeChecked();
    expect(onSaved).not.toHaveBeenCalled();
  });
});
