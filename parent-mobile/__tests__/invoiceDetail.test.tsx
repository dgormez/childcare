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

jest.mock("../services/payments", () => ({
  createPaymentLink: jest.fn(),
  getPaymentStatus: jest.fn(),
  downloadBetalingsbewijs: jest.fn(),
}));

jest.mock("expo-sharing", () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  shareAsync: jest.fn().mockResolvedValue(undefined),
}));

jest.mock("expo-web-browser", () => ({
  openBrowserAsync: jest.fn().mockResolvedValue({ type: "dismiss" }),
}));

const { getInvoices, downloadInvoicePdf } = jest.requireMock("../services/invoices") as {
  getInvoices: jest.Mock;
  downloadInvoicePdf: jest.Mock;
};
const { createPaymentLink, getPaymentStatus, downloadBetalingsbewijs } = jest.requireMock("../services/payments") as {
  createPaymentLink: jest.Mock;
  getPaymentStatus: jest.Mock;
  downloadBetalingsbewijs: jest.Mock;
};
const Sharing = jest.requireMock("expo-sharing") as { isAvailableAsync: jest.Mock; shareAsync: jest.Mock };
const WebBrowser = jest.requireMock("expo-web-browser") as { openBrowserAsync: jest.Mock };
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

const paidInvoice: ParentInvoiceEntry = { ...invoice, status: "paid", paidAt: "2026-07-20T00:00:00Z" };

describe("online payment (feature 014a)", () => {
  it("shows Pay now on a Sent invoice, not View receipt", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [invoice] });
    const { findByText, queryByText } = await render(<InvoiceDetailScreen />);
    expect(await findByText("invoices.payNow")).toBeTruthy();
    expect(queryByText("invoices.viewReceipt")).toBeNull();
  });

  it("shows View receipt on a Paid invoice, not Pay now", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [paidInvoice] });
    const { findByText, queryByText } = await render(<InvoiceDetailScreen />);
    expect(await findByText("invoices.viewReceipt")).toBeTruthy();
    expect(queryByText("invoices.payNow")).toBeNull();
  });

  it("tapping Pay now opens the checkout page and resolves to Paid once confirmed", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [invoice] });
    createPaymentLink.mockResolvedValue({ status: "created", checkoutUrl: "https://fake-mollie.test/checkout/abc" });
    getPaymentStatus.mockResolvedValue({ invoiceStatus: "paid", paymentStatus: "paid" });

    const { findByText, queryByText } = await render(<InvoiceDetailScreen />);
    await fireEvent.press(await findByText("invoices.payNow"));

    await waitFor(() => expect(createPaymentLink).toHaveBeenCalledWith("inv-1"));
    await waitFor(() => expect(WebBrowser.openBrowserAsync).toHaveBeenCalledWith("https://fake-mollie.test/checkout/abc"));
    await waitFor(() => expect(getPaymentStatus).toHaveBeenCalledWith("inv-1"), { timeout: 5000 });
    await waitFor(() => expect(queryByText("invoices.confirmingPayment")).toBeNull(), { timeout: 5000 });
    expect(await findByText("invoices.viewReceipt")).toBeTruthy();
  }, 10000);

  it("shows a not-connected message instead of Pay now when the organisation has no Mollie connection", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [invoice] });
    createPaymentLink.mockResolvedValue({ status: "not_connected" });

    const { findByText, queryByText } = await render(<InvoiceDetailScreen />);
    await fireEvent.press(await findByText("invoices.payNow"));

    expect(await findByText("invoices.payNotAvailable")).toBeTruthy();
    expect(queryByText("invoices.payNow")).toBeNull();
  });

  it("shows a retry-capable error when creating the payment link fails", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [invoice] });
    createPaymentLink.mockResolvedValue({ status: "error" });

    const { findByText } = await render(<InvoiceDetailScreen />);
    await fireEvent.press(await findByText("invoices.payNow"));

    expect(await findByText("invoices.payLinkFailed")).toBeTruthy();
    expect(await findByText("invoices.payNow")).toBeTruthy();
  });

  it("downloads and shares the betalingsbewijs from a Paid invoice", async () => {
    getInvoices.mockResolvedValue({ status: "loaded", invoices: [paidInvoice] });
    downloadBetalingsbewijs.mockResolvedValue({ uri: "file:///cache/receipts/inv-1-betalingsbewijs.pdf" });

    const { findByText } = await render(<InvoiceDetailScreen />);
    await fireEvent.press(await findByText("invoices.viewReceipt"));

    await waitFor(() => expect(downloadBetalingsbewijs).toHaveBeenCalledWith("inv-1"));
    await waitFor(() =>
      expect(Sharing.shareAsync).toHaveBeenCalledWith("file:///cache/receipts/inv-1-betalingsbewijs.pdf", expect.any(Object)),
    );
  });
});
