import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { InvoiceSettingsForm } from "../components/InvoiceSettingsForm";
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
  erkenningsnummer: "KDV12345",
  bankAccountNumber: "BE68 5390 0754 7034",
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
      <InvoiceSettingsForm location={location} onSaved={onSaved} />
    </NextIntlClientProvider>,
  );
  return onSaved;
}

beforeEach(() => {
  vi.mocked(apiClient.PUT).mockReset();
});

describe("InvoiceSettingsForm", () => {
  it("loads the current erkenningsnummer/bank account number/due-days values", () => {
    renderForm();
    expect(screen.getByLabelText("Erkenningsnummer")).toHaveValue("KDV12345");
    expect(screen.getByLabelText("Bank account number")).toHaveValue("BE68 5390 0754 7034");
    expect(screen.getByLabelText("Payment due within (days)")).toHaveValue(14);
  });

  it("saves changes and reflects the updated location via onSaved", async () => {
    const updated = { ...location, erkenningsnummer: "KDV99999", invoiceDueDays: 30 };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.clear(screen.getByLabelText("Erkenningsnummer"));
    await userEvent.type(screen.getByLabelText("Erkenningsnummer"), "KDV99999");
    await userEvent.clear(screen.getByLabelText("Payment due within (days)"));
    await userEvent.type(screen.getByLabelText("Payment due within (days)"), "30");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/invoice-settings",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: { erkenningsnummer: "KDV99999", bankAccountNumber: "BE68 5390 0754 7034", invoiceDueDays: 30 },
      }),
    );
    expect(screen.getByText("Invoicing settings saved.")).toBeInTheDocument();
  });

  it("shows an error notice on a failed save", async () => {
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse(404) as never);
    const onSaved = renderForm();

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Couldn't save changes. Please try again.")).toBeInTheDocument();
    expect(onSaved).not.toHaveBeenCalled();
  });
});

describe("InvoiceSettingsForm — payment reminder settings", () => {
  it("loads the current reminder settings (disabled, default delay/cadence)", () => {
    renderForm();
    expect(screen.getByLabelText("Send automatic payment reminders")).not.toBeChecked();
    expect(screen.getByLabelText("First reminder after (days overdue)")).toHaveValue(3);
    expect(screen.getByLabelText("Repeat every (days)")).toHaveValue(7);
  });

  it("saves reminder settings independently of the invoice-settings fields", async () => {
    const updated = { ...location, paymentRemindersEnabled: true, paymentReminderDelayDays: 5, paymentReminderCadenceDays: 10 };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.click(screen.getByLabelText("Send automatic payment reminders"));
    await userEvent.clear(screen.getByLabelText("First reminder after (days overdue)"));
    await userEvent.type(screen.getByLabelText("First reminder after (days overdue)"), "5");
    await userEvent.clear(screen.getByLabelText("Repeat every (days)"));
    await userEvent.type(screen.getByLabelText("Repeat every (days)"), "10");
    await userEvent.click(screen.getByRole("button", { name: "Save reminder settings" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/payment-reminder-settings",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: { enabled: true, delayDays: 5, cadenceDays: 10 },
      }),
    );
    expect(screen.getByText("Reminder settings saved.")).toBeInTheDocument();
  });

  it("shows an error notice and reverts the toggle on a failed reminder-settings save", async () => {
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse(404) as never);
    renderForm();

    await userEvent.click(screen.getByLabelText("Send automatic payment reminders"));
    await userEvent.click(screen.getByRole("button", { name: "Save reminder settings" }));

    expect(await screen.findByText("Couldn't save changes. Please try again.")).toBeInTheDocument();
    expect(screen.getByLabelText("Send automatic payment reminders")).not.toBeChecked();
  });
});

describe("InvoiceSettingsForm — sibling billing settings", () => {
  it("loads the current discount percent and bundling toggle (defaults: 0%, disabled)", () => {
    renderForm();
    expect(screen.getByLabelText("Sibling discount (%)")).toHaveValue(0);
    expect(screen.getByLabelText("Combine sibling invoices into one")).not.toBeChecked();
  });

  it("saves sibling billing settings independently of the other fields", async () => {
    const updated = { ...location, siblingDiscountPct: 10, familyInvoiceBundlingEnabled: true };
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse(updated) as never);
    const onSaved = renderForm();

    await userEvent.clear(screen.getByLabelText("Sibling discount (%)"));
    await userEvent.type(screen.getByLabelText("Sibling discount (%)"), "10");
    await userEvent.click(screen.getByLabelText("Combine sibling invoices into one"));
    await userEvent.click(screen.getByRole("button", { name: "Save sibling billing settings" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated));
    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/locations/{id}/sibling-billing-settings",
      expect.objectContaining({
        params: { path: { id: "loc-1" } },
        body: { siblingDiscountPct: 10, familyInvoiceBundlingEnabled: true },
      }),
    );
    expect(screen.getByText("Sibling billing settings saved.")).toBeInTheDocument();
  });

  it("shows an error notice and reverts the toggle on a failed sibling-billing save", async () => {
    vi.mocked(apiClient.PUT).mockResolvedValue(errorResponse(404) as never);
    renderForm();

    await userEvent.click(screen.getByLabelText("Combine sibling invoices into one"));
    await userEvent.click(screen.getByRole("button", { name: "Save sibling billing settings" }));

    expect(await screen.findByText("Couldn't save changes. Please try again.")).toBeInTheDocument();
    expect(screen.getByLabelText("Combine sibling invoices into one")).not.toBeChecked();
  });
});
