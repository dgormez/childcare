import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { ReservationSettingsForm } from "../components/ReservationSettingsForm";
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
  deactivatedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number, errorKey: string, extra: Record<string, unknown> = {}) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey, ...extra } };
}

function renderForm(onSaved = vi.fn()) {
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <ReservationSettingsForm location={location} onSaved={onSaved} />
    </NextIntlClientProvider>,
  );
  return onSaved;
}

beforeEach(() => {
  vi.mocked(apiClient.PUT).mockReset();
});

describe("ReservationSettingsForm", () => {
  it("loads current values into the three mode selects and the notice-hours field", () => {
    renderForm();
    expect(screen.getByLabelText("Absence reports")).toHaveValue("approval");
    expect(screen.getByLabelText("Extra day requests")).toHaveValue("approval");
    expect(screen.getByLabelText("Day exchange requests")).toHaveValue("disabled");
    expect(screen.getByLabelText("Minimum notice (hours)")).toHaveValue(0);
  });

  it("saves a mode change and reflects the updated value via onSaved", async () => {
    const updated = { ...location, reservationAbsencesMode: "disabled" as const };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.selectOptions(screen.getByLabelText("Absence reports"), "disabled");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/reservation-settings",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: expect.objectContaining({ absencesMode: "disabled", confirmDespitePending: false }),
      }),
    );
  });

  it("shows the pending-requests warning dialog and resubmits with confirmDespitePending on confirm", async () => {
    vi.mocked(apiClient.PUT)
      .mockResolvedValueOnce(errorResponse(409, "errors.location.reservation_settings.pending_requests_warning", {
        pendingCounts: { absence: 2 },
      }) as never)
      .mockResolvedValueOnce(okResponse({ ...location, reservationAbsencesMode: "disabled" }) as never);
    renderForm();

    await userEvent.selectOptions(screen.getByLabelText("Absence reports"), "disabled");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Pending requests won't be affected")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Save anyway" }));

    await waitFor(() => expect(apiClient.PUT).toHaveBeenCalledTimes(2));
    expect(apiClient.PUT).toHaveBeenLastCalledWith(
      "/api/locations/{id}/reservation-settings",
      expect.objectContaining({ body: expect.objectContaining({ confirmDespitePending: true }) }),
    );
  });
});
