import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { MenuVariantSettingsForm } from "../components/MenuVariantSettingsForm";
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
  menuVariantPriorityOrder: ["halal", "vegan"],
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
      <MenuVariantSettingsForm location={location} onSaved={onSaved} />
    </NextIntlClientProvider>,
  );
  return onSaved;
}

beforeEach(() => {
  vi.mocked(apiClient.PUT).mockReset();
});

describe("MenuVariantSettingsForm", () => {
  it("loads the current menuVariantPriorityOrder into checked, ordered checkboxes", () => {
    renderForm();
    expect(screen.getByRole("checkbox", { name: "Halal" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Vegan" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Kosher" })).not.toBeChecked();
  });

  it("enabling a variant and reordering calls the endpoint with the updated priority order", async () => {
    const updated = { ...location, menuVariantPriorityOrder: ["halal", "kosher", "vegan"] };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.click(screen.getByRole("checkbox", { name: "Kosher" }));
    const moveUpButtons = screen.getAllByRole("button", { name: "Move up" });
    await userEvent.click(moveUpButtons[moveUpButtons.length - 1]);
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/menu-variant-settings",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: expect.objectContaining({ confirmDespiteRemovingPublished: false }),
      }),
    );
  });

  it("shows the removal warning dialog and resubmits with confirmDespiteRemovingPublished on confirm", async () => {
    vi.mocked(apiClient.PUT)
      .mockResolvedValueOnce(errorResponse(409, "errors.location.menu_variant_settings.removing_published_warning", {
        variants: ["halal"],
      }) as never)
      .mockResolvedValueOnce(okResponse({ ...location, menuVariantPriorityOrder: ["vegan"] }) as never);
    renderForm();

    await userEvent.click(screen.getByRole("checkbox", { name: "Halal" }));
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("This variant is currently visible to families")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Remove anyway" }));

    await waitFor(() => expect(apiClient.PUT).toHaveBeenCalledTimes(2));
    expect(apiClient.PUT).toHaveBeenLastCalledWith(
      "/api/locations/{id}/menu-variant-settings",
      expect.objectContaining({ body: expect.objectContaining({ confirmDespiteRemovingPublished: true }) }),
    );
  });
});
