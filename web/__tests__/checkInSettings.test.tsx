import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { CheckInSettingsForm } from "../components/CheckInSettingsForm";
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
      <CheckInSettingsForm location={location} onSaved={onSaved} />
    </NextIntlClientProvider>,
  );
  return onSaved;
}

beforeEach(() => {
  vi.mocked(apiClient.PUT).mockReset();
});

describe("CheckInSettingsForm", () => {
  it("loads the current toggle state and shows the tradeoff copy alongside it", () => {
    renderForm();
    expect(screen.getByLabelText("PIN required at check-in/check-out")).toBeChecked();
    expect(screen.getByText(/identity at check-in\/out will no longer be confirmed/)).toBeInTheDocument();
    expect(screen.getByText(/if a caregiver taps the wrong card by mistake/)).toBeInTheDocument();
  });

  it("saves a toggle change and reflects the updated value via onSaved", async () => {
    const updated = { ...location, requiresCaregiverPin: false };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.click(screen.getByLabelText("PIN required at check-in/check-out"));
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/checkin-settings",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: { requiresCaregiverPin: false },
      }),
    );
  });

  it("reverts the toggle and shows an error notice on a failed save (FR-015)", async () => {
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse(404) as never);
    const onSaved = renderForm();

    const toggle = screen.getByLabelText("PIN required at check-in/check-out");
    await userEvent.click(toggle);
    expect(toggle).not.toBeChecked();

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Couldn't save changes. Please try again.")).toBeInTheDocument();
    expect(toggle).toBeChecked();
    expect(onSaved).not.toHaveBeenCalled();
  });
});
