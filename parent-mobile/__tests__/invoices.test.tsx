import React from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react-native";
import InvoicesScreen from "../app/(app)/invoices/index";
import type { ParentFamilyInvoiceEntry, ParentInvoiceEntry } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts ? `${key} ${JSON.stringify(opts)}` : key) }),
}));

jest.mock("../services/invoices", () => ({
  getInvoices: jest.fn(),
  downloadFamilyInvoicePdf: jest.fn(),
}));

jest.mock("../services/payments", () => ({
  createPaymentLink: jest.fn(),
  getPaymentStatus: jest.fn(),
}));

jest.mock("expo-web-browser", () => ({
  openBrowserAsync: jest.fn().mockResolvedValue({ type: "dismiss" }),
}));

const { getInvoices } = jest.requireMock("../services/invoices") as { getInvoices: jest.Mock };
const { createPaymentLink, getPaymentStatus } = jest.requireMock("../services/payments") as {
  createPaymentLink: jest.Mock;
  getPaymentStatus: jest.Mock;
};
const WebBrowser = jest.requireMock("expo-web-browser") as { openBrowserAsync: jest.Mock };

function makeInvoice(overrides: Partial<ParentInvoiceEntry> = {}): ParentInvoiceEntry {
  return {
    id: "inv-1",
    childId: "child-1",
    childName: "Emma Peeters",
    contractId: "contract-1",
    locationId: "loc-1",
    locationName: "Sunshine House",
    periodMonth: "2026-07",
    status: "sent",
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
    ogmReference: "+++097/0000/00017+++",
    dueDate: "2026-07-29",
    sentAt: "2026-07-15T00:00:00Z",
    paidAt: null,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: "2026-07-01T00:00:00Z",
    ...overrides,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
});

it("shows sent and paid invoices correctly attributed to each child with a plain-language status", async () => {
  const invoices: ParentInvoiceEntry[] = [
    makeInvoice({ id: "inv-1", childName: "Emma Peeters", status: "sent" }),
    makeInvoice({ id: "inv-2", childId: "child-2", childName: "Liam Peeters", status: "paid", paidAt: "2026-07-20T00:00:00Z" }),
  ];
  getInvoices.mockResolvedValue({ status: "loaded", invoices });

  const { findByText } = await render(<InvoicesScreen />);

  expect(await findByText("Emma Peeters")).toBeTruthy();
  expect(await findByText("invoices.statusSent")).toBeTruthy();
  expect(await findByText("Liam Peeters")).toBeTruthy();
  expect(await findByText("invoices.statusPaid")).toBeTruthy();
});

it("shows an overdue invoice with the overdue status, not sent", async () => {
  getInvoices.mockResolvedValue({
    status: "loaded",
    invoices: [makeInvoice({ status: "sent", isOverdue: true })],
  });

  const { findByText, queryByText } = await render(<InvoicesScreen />);

  expect(await findByText("invoices.statusOverdue")).toBeTruthy();
  expect(queryByText("invoices.statusSent")).toBeNull();
});

it("shows an empty state when there are no invoices", async () => {
  getInvoices.mockResolvedValue({ status: "loaded", invoices: [] });

  const { findByText } = await render(<InvoicesScreen />);
  expect(await findByText("invoices.noInvoices")).toBeTruthy();
});

it("shows an unavailable message when the service can't load invoices", async () => {
  getInvoices.mockResolvedValue({ status: "unavailable" });

  const { findByText } = await render(<InvoicesScreen />);
  expect(await findByText("invoices.loadFailed")).toBeTruthy();
});

function makeFamilyEntry(overrides: Partial<ParentFamilyInvoiceEntry> = {}): ParentFamilyInvoiceEntry {
  return {
    familyGroupId: "family-1",
    children: [
      { invoiceId: "inv-1", childId: "child-1", childName: "Emma Peeters", subtotalCents: 45000 },
      { invoiceId: "inv-2", childId: "child-2", childName: "Liam Peeters", subtotalCents: 40500 },
    ],
    totalCents: 85500,
    status: "sent",
    isOverdue: false,
    dueDate: "2026-07-29",
    createdAt: "2026-07-01T00:00:00Z",
    ...overrides,
  };
}

// Feature 030 (US3) — a FamilyGroupId entry renders distinctly from a normal single-invoice entry.
it("renders a grouped family invoice entry distinctly from a normal single-invoice entry", async () => {
  const familyEntry = makeFamilyEntry();
  const normalInvoice = makeInvoice({ id: "inv-3", childId: "child-3", childName: "Nora Peeters" });
  getInvoices.mockResolvedValue({ status: "loaded", invoices: [familyEntry, normalInvoice] });

  const { findByText } = await render(<InvoicesScreen />);

  expect(await findByText("Nora Peeters")).toBeTruthy();
  expect(await screen.findAllByText(/invoices\.familyGroup\.perChildLine/)).toHaveLength(2);
  expect(await findByText("invoices.familyGroup.combinedTotal")).toBeTruthy();
});

// Feature 030 Convergence (T073/T074) — spec.md FR-009a/Clarifications: paying any one invoice
// in the bundle via the family tile's Pay action must be reachable from this list screen (no
// per-invoice detail navigation exists for a grouped entry) and reflect the whole group Paid.
describe("online payment for a bundled family invoice (feature 030 convergence)", () => {
  it("shows a Pay action on a Sent family entry", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [makeFamilyEntry()] });

    const { findByLabelText } = await render(<InvoicesScreen />);
    expect(await findByLabelText("invoices.payNow")).toBeTruthy();
  });

  it("does not show a Pay action on a Paid family entry", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [makeFamilyEntry({ status: "paid" })] });

    const { queryByLabelText } = await render(<InvoicesScreen />);
    expect(queryByLabelText("invoices.payNow")).toBeNull();
  });

  it("tapping Pay uses the first grouped child's invoiceId, opens checkout, and reloads the list once confirmed", async () => {
    getInvoices.mockResolvedValueOnce({ status: "loaded", invoices: [makeFamilyEntry()] });
    createPaymentLink.mockResolvedValue({ status: "created", checkoutUrl: "https://fake-mollie.test/checkout/abc" });
    getPaymentStatus.mockResolvedValue({ invoiceStatus: "paid", paymentStatus: "paid" });
    getInvoices.mockResolvedValueOnce({ status: "loaded", invoices: [makeFamilyEntry({ status: "paid" })] });

    const { findByLabelText, findAllByText } = await render(<InvoicesScreen />);
    await fireEvent.press(await findByLabelText("invoices.payNow"));

    await waitFor(() => expect(createPaymentLink).toHaveBeenCalledWith("inv-1"));
    await waitFor(() => expect(WebBrowser.openBrowserAsync).toHaveBeenCalledWith("https://fake-mollie.test/checkout/abc"));
    await waitFor(() => expect(getPaymentStatus).toHaveBeenCalledWith("inv-1"), { timeout: 5000 });
    await waitFor(() => expect(getInvoices).toHaveBeenCalledTimes(2), { timeout: 5000 });
    expect(await findAllByText("invoices.statusPaid")).toHaveLength(1);
  }, 10000);

  it("shows a not-connected message when the organisation has no Mollie connection", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [makeFamilyEntry()] });
    createPaymentLink.mockResolvedValue({ status: "not_connected" });

    const { findByLabelText, findByText } = await render(<InvoicesScreen />);
    await fireEvent.press(await findByLabelText("invoices.payNow"));

    expect(await findByText("invoices.payNotAvailable")).toBeTruthy();
  });
});
