import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import InvoicesPage from "../app/(app)/invoices/page";
import { apiClient } from "../lib/apiClient";
import type { InvoiceResponse, LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <InvoicesPage />
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
  deactivatedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function makeInvoice(overrides: Partial<InvoiceResponse> = {}): InvoiceResponse {
  return {
    id: "inv-1",
    childId: "child-1",
    childName: "Emma Peeters",
    contractId: "contract-1",
    locationId: "loc-1",
    locationName: "Sunshine House",
    periodMonth: "2026-07",
    status: "draft",
    isOverdue: false,
    subtotalCents: 45000,
    totalCents: 45000,
    lineItems: {
      presentDays: 15,
      unjustifiedAbsentDays: 0,
      dailyRateCents: 3000,
      closureDaysExcluded: 0,
      daysMin5u: 0,
      daysMin11u: 15,
      extraCharges: [],
    },
    ogmReference: "",
    dueDate: null,
    sentAt: null,
    paidAt: null,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: "2026-07-01T00:00:00Z",
    ...overrides,
  };
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey: "errors.invoice.generation_failed" } };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  push.mockReset();
});

describe("InvoicesPage", () => {
  it("shows an empty state when no invoices exist yet for the selected location/month", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([])) as ReturnType<typeof apiClient.GET>;
    });
    renderPage();

    expect(await screen.findByText("No invoices generated yet for this location and month.")).toBeInTheDocument();
  });

  it("renders the resulting table rows with correct amounts/statuses after generating invoices", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([])) as ReturnType<typeof apiClient.GET>;
    });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse([makeInvoice()]) as never);
    renderPage();

    await screen.findByText("No invoices generated yet for this location and month.");
    await userEvent.click(screen.getByRole("button", { name: "Generate invoices" }));

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("€450.00")).toBeInTheDocument();
    expect(within(screen.getByRole("table")).getByText("Draft")).toBeInTheDocument();
    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/locations/{locationId}/invoices/generate",
      expect.objectContaining({ params: { path: { locationId: "loc-1" } } }),
    );
  });

  it("navigates to the invoice detail page from anywhere in the row (full-row click affordance)", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([makeInvoice()])) as ReturnType<typeof apiClient.GET>;
    });
    renderPage();

    await userEvent.click(await screen.findByText("€450.00"));

    expect(push).toHaveBeenCalledWith("/invoices/inv-1");
  });

  it("re-fetches with the selected status filter (FR-015)", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(okResponse([])) as ReturnType<typeof apiClient.GET>;
    });
    renderPage();

    await screen.findByText("No invoices generated yet for this location and month.");
    vi.mocked(apiClient.GET).mockClear();

    await userEvent.selectOptions(screen.getByLabelText("Status"), "paid");

    await waitFor(() =>
      expect(apiClient.GET).toHaveBeenCalledWith(
        "/api/locations/{locationId}/invoices",
        expect.objectContaining({ params: expect.objectContaining({ query: expect.objectContaining({ status: "paid" }) }) }),
      ),
    );
  });

  it("shows an error state when loading invoices fails", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([location])) as ReturnType<typeof apiClient.GET>;
      return Promise.resolve(errorResponse(500)) as ReturnType<typeof apiClient.GET>;
    });
    renderPage();

    expect(await screen.findByText("Couldn't load invoices. Please try again.")).toBeInTheDocument();
  });
});
