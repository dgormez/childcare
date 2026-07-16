import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import FiscalAttestationsScreen from "../app/(app)/fiscal-attestations/index";
import type { FiscalAttestationResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts?.year !== undefined ? `${key}:${opts.year}` : key) }),
}));

jest.mock("../services/fiscalAttestations", () => ({
  getFiscalAttestations: jest.fn(),
  downloadFiscalAttestationPdf: jest.fn(),
}));

jest.mock("expo-sharing", () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  shareAsync: jest.fn().mockResolvedValue(undefined),
}));

const { getFiscalAttestations, downloadFiscalAttestationPdf } = jest.requireMock("../services/fiscalAttestations") as {
  getFiscalAttestations: jest.Mock;
  downloadFiscalAttestationPdf: jest.Mock;
};
const Sharing = jest.requireMock("expo-sharing") as { isAvailableAsync: jest.Mock; shareAsync: jest.Mock };

function makeAttestation(overrides: Partial<FiscalAttestationResponse> = {}): FiscalAttestationResponse {
  return {
    id: "att-1",
    childId: "child-1",
    childName: "Emma Peeters",
    locationId: "loc-1",
    locationName: "Sunshine House",
    taxYear: 2026,
    totalAmountCents: 84000,
    status: "generated",
    periods: [{ periodStart: "2026-01-01", periodEnd: "2026-12-31", days: 24, amountCents: 84000, dailyRateCents: 3500 }],
    generatedAt: "2027-01-15T10:00:00Z",
    ...overrides,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
});

it("shows the caller's attestations with child, location, tax year, and amount", async () => {
  getFiscalAttestations.mockResolvedValue({
    status: "loaded",
    attestations: [
      makeAttestation({ id: "att-1", childName: "Emma Peeters" }),
      makeAttestation({ id: "att-2", childId: "child-2", childName: "Liam Peeters", locationName: "Second Site" }),
    ],
  });

  const { findByText } = await render(<FiscalAttestationsScreen />);

  expect(await findByText("Emma Peeters")).toBeTruthy();
  expect(await findByText("Liam Peeters")).toBeTruthy();
  // Location name is a sibling text node within the same <Text>, alongside the tax-year label —
  // matched by substring since the full node text is "fiscalAttestations.taxYear:2026 · Second Site".
  expect(await findByText(/Second Site/)).toBeTruthy();
});

it("shows the not-available-yet empty state when no attestations exist", async () => {
  getFiscalAttestations.mockResolvedValue({ status: "loaded", attestations: [] });

  const { findByText } = await render(<FiscalAttestationsScreen />);
  expect(await findByText("fiscalAttestations.notAvailableYet")).toBeTruthy();
});

it("shows an unavailable message when the service can't load attestations", async () => {
  getFiscalAttestations.mockResolvedValue({ status: "unavailable" });

  const { findByText } = await render(<FiscalAttestationsScreen />);
  expect(await findByText("fiscalAttestations.loadFailed")).toBeTruthy();
});

it("tapping an attestation downloads the PDF and opens the share sheet", async () => {
  getFiscalAttestations.mockResolvedValue({ status: "loaded", attestations: [makeAttestation()] });
  downloadFiscalAttestationPdf.mockResolvedValue({ uri: "file:///cache/fiscal-attestations/att-1.pdf" });

  const { findByText } = await render(<FiscalAttestationsScreen />);
  fireEvent.press(await findByText("Emma Peeters"));

  await waitFor(() => expect(downloadFiscalAttestationPdf).toHaveBeenCalledWith("att-1"));
  await waitFor(() => expect(Sharing.shareAsync).toHaveBeenCalledWith("file:///cache/fiscal-attestations/att-1.pdf", expect.any(Object)));
});

it("shows a download error message when the download fails", async () => {
  getFiscalAttestations.mockResolvedValue({ status: "loaded", attestations: [makeAttestation()] });
  downloadFiscalAttestationPdf.mockRejectedValue(new Error("network"));

  const { findByText } = await render(<FiscalAttestationsScreen />);
  fireEvent.press(await findByText("Emma Peeters"));

  expect(await findByText("fiscalAttestations.downloadFailed")).toBeTruthy();
});
