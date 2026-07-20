import React from "react";
import { render } from "@testing-library/react-native";
import ScanScreen from "../../app/(app)/scan";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/attendance", () => ({
  scanCheckInCode: jest.fn(),
}));

const { useCameraPermissions } = require("expo-camera");

beforeEach(() => {
  jest.clearAllMocks();
});

// Feature 021, FR-013/T041 — the tablet's scan screen must surface a working path back to
// manual tap when the camera is unavailable, rather than a dead end.
describe("scan screen camera-unavailable fallback (feature 021)", () => {
  it("shows the camera-unavailable fallback and a manual-fallback action when permission is denied", async () => {
    useCameraPermissions.mockReturnValue([{ granted: false, canAskAgain: false }, jest.fn(), jest.fn()]);

    const { getByText, queryByTestId } = await render(<ScanScreen />);

    expect(getByText("qrCheckIn.cameraUnavailable")).toBeTruthy();
    expect(getByText("qrCheckIn.useManualInstead")).toBeTruthy();
    expect(queryByTestId("camera-view")).toBeNull();
  });

  it("renders the camera viewfinder once permission is granted", async () => {
    useCameraPermissions.mockReturnValue([{ granted: true, canAskAgain: true }, jest.fn(), jest.fn()]);

    const { getByTestId } = await render(<ScanScreen />);

    expect(getByTestId("camera-view")).toBeTruthy();
  });
});
