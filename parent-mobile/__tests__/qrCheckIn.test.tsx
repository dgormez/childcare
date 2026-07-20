import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import * as Network from "expo-network";
import QrCheckInScreen from "../app/(app)/qr-checkin/[childId]";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: { name?: string }) => (opts?.name ? `${key}:${opts.name}` : key) }),
}));

jest.mock("expo-router", () => ({
  useLocalSearchParams: () => ({ childId: "c1", name: "Timmy" }),
}));

jest.mock("../services/attendance", () => ({
  requestQrCode: jest.fn(),
}));

const { requestQrCode } = jest.requireMock("../services/attendance") as { requestQrCode: jest.Mock };

beforeEach(() => {
  jest.clearAllMocks();
  (Network.getNetworkStateAsync as jest.Mock).mockResolvedValue({ isConnected: true, isInternetReachable: true });
});

// Feature 021, T049 — the parent-mobile QR code-display screen's loading/success/error-retry/
// offline states (spec.md UX Requirements).
describe("QrCheckInScreen", () => {
  it("shows a loading state, then renders the QR code once issued (FR-005)", async () => {
    requestQrCode.mockResolvedValue({ code: "payload.signature", expiresAtUnix: 1721472930 });

    const { getByText, queryByText } = await render(<QrCheckInScreen />);

    await waitFor(() => expect(queryByText("qrCheckIn.loading")).toBeNull());
    expect(getByText("qrCheckIn.title:Timmy")).toBeTruthy();
    expect(requestQrCode).toHaveBeenCalledWith("c1");
  });

  it("shows an error with a retry action when issuance fails, and retrying re-issues (FR-018-style)", async () => {
    requestQrCode.mockRejectedValueOnce(new Error("errors.qrCheckIn.issue_failed"));
    requestQrCode.mockResolvedValueOnce({ code: "payload.signature", expiresAtUnix: 1721472930 });

    const { getByText, queryByText } = await render(<QrCheckInScreen />);

    expect(await waitFor(() => getByText("qrCheckIn.issueFailed"))).toBeTruthy();

    fireEvent.press(getByText("qrCheckIn.retry"));

    await waitFor(() => expect(queryByText("qrCheckIn.issueFailed")).toBeNull());
    expect(requestQrCode).toHaveBeenCalledTimes(2);
  });

  it("shows the offline state and never attempts issuance while offline (research.md R6)", async () => {
    (Network.getNetworkStateAsync as jest.Mock).mockResolvedValue({ isConnected: false, isInternetReachable: false });

    const { findByText } = await render(<QrCheckInScreen />);

    expect(await findByText("qrCheckIn.offline")).toBeTruthy();
    expect(requestQrCode).not.toHaveBeenCalled();
  });
});
