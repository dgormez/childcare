import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import InvoiceDetailScreen from "../app/(app)/invoices/[id]";
import type { ParentInvoiceEntry } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, options?: Record<string, unknown>) => (options ? `${key}:${JSON.stringify(options)}` : key) }),
}));

jest.mock("../services/invoices", () => ({
  getInvoices: jest.fn(),
  downloadInvoicePdf: jest.fn(),
}));

jest.mock("expo-sharing", () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  shareAsync: jest.fn().mockResolvedValue(undefined),
}));

const { getInvoices, downloadInvoicePdf } = jest.requireMock("../services/invoices") as {
  getInvoices: jest.Mock;
  downloadInvoicePdf: jest.Mock;
};
const Sharing = jest.requireMock("expo-sharing") as { isAvailableAsync: jest.Mock; shareAsync: jest.Mock };
const { useLocalSearchParams } = require("expo-router");

const invoice: ParentInvoiceEntry = {
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
};

beforeEach(() => {
  jest.clearAllMocks();
  useLocalSearchParams.mockReturnValue({ id: "inv-1" });
});

it("renders the invoice's OGM reference and downloads/shares the PDF on request", async () => {
  getInvoices.mockResolvedValue({ status: "loaded", invoices: [invoice] });
  downloadInvoicePdf.mockResolvedValue({ uri: "file:///cache/invoices/inv-1.pdf" });

  const { findByText } = await render(<InvoiceDetailScreen />);

  expect(await findByText("Emma Peeters")).toBeTruthy();
  expect(await findByText("+++097/0000/00017+++")).toBeTruthy();

  await fireEvent.press(await findByText("invoices.downloadPdf"));

  await waitFor(() => expect(downloadInvoicePdf).toHaveBeenCalledWith("inv-1"));
  await waitFor(() => expect(Sharing.shareAsync).toHaveBeenCalledWith("file:///cache/invoices/inv-1.pdf", expect.any(Object)));
});

it("shows a download error notice when the PDF fetch fails", async () => {
  getInvoices.mockResolvedValue({ status: "loaded", invoices: [invoice] });
  downloadInvoicePdf.mockRejectedValue(new Error("network"));

  const { findByText } = await render(<InvoiceDetailScreen />);
  await fireEvent.press(await findByText("invoices.downloadPdf"));

  expect(await findByText("invoices.downloadFailed")).toBeTruthy();
});

it("shows a load-failed message when the invoice isn't found", async () => {
  getInvoices.mockResolvedValue({ status: "loaded", invoices: [] });

  const { findByText } = await render(<InvoiceDetailScreen />);
  expect(await findByText("invoices.loadFailed")).toBeTruthy();
});
