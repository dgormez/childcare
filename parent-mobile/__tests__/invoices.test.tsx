import React from "react";
import { render, screen } from "@testing-library/react-native";
import InvoicesScreen from "../app/(app)/invoices/index";
import type { ParentFamilyInvoiceEntry, ParentInvoiceEntry } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts ? `${key} ${JSON.stringify(opts)}` : key) }),
}));

jest.mock("../services/invoices", () => ({
  getInvoices: jest.fn(),
  downloadFamilyInvoicePdf: jest.fn(),
}));

const { getInvoices } = jest.requireMock("../services/invoices") as { getInvoices: jest.Mock };

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

// Feature 030 (US3) — a FamilyGroupId entry renders distinctly from a normal single-invoice entry.
it("renders a grouped family invoice entry distinctly from a normal single-invoice entry", async () => {
  const familyEntry: ParentFamilyInvoiceEntry = {
    familyGroupId: "family-1",
    children: [
      { childId: "child-1", childName: "Emma Peeters", subtotalCents: 45000 },
      { childId: "child-2", childName: "Liam Peeters", subtotalCents: 40500 },
    ],
    totalCents: 85500,
    status: "sent",
    isOverdue: false,
    dueDate: "2026-07-29",
    createdAt: "2026-07-01T00:00:00Z",
  };
  const normalInvoice = makeInvoice({ id: "inv-3", childId: "child-3", childName: "Nora Peeters" });
  getInvoices.mockResolvedValue({ status: "loaded", invoices: [familyEntry, normalInvoice] });

  const { findByText } = await render(<InvoicesScreen />);

  expect(await findByText("Nora Peeters")).toBeTruthy();
  expect(await screen.findAllByText(/invoices\.familyGroup\.perChildLine/)).toHaveLength(2);
  expect(await findByText("invoices.familyGroup.combinedTotal")).toBeTruthy();
});
