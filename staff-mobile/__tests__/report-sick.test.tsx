import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import ReportSickScreen from "../app/(app)/report-sick";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
jest.mock("../services/schedule", () => ({
  reportSick: jest.fn(),
}));
jest.mock("../hooks/useIsOffline", () => ({
  useIsOffline: jest.fn(() => false),
}));

const { reportSick } = require("../services/schedule");
const { useIsOffline } = require("../hooks/useIsOffline");
const { useRouter } = require("expo-router");

beforeEach(() => {
  jest.clearAllMocks();
  useIsOffline.mockReturnValue(false);
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

// FR-005/SC-002: one tap plus a confirmation step, not a form.
it("requires an explicit confirmation step before submitting", async () => {
  reportSick.mockResolvedValue({ succeeded: true });
  const { getByTestId } = await render(<ReportSickScreen />);

  expect(reportSick).not.toHaveBeenCalled();
  await fireEvent.press(getByTestId("report-sick-cta"));
  expect(reportSick).not.toHaveBeenCalled(); // still not called — confirmation screen only

  await fireEvent.press(await waitFor(() => getByTestId("report-sick-confirm")));
  await waitFor(() => expect(reportSick).toHaveBeenCalled());
});

it("shows a success message after confirming", async () => {
  reportSick.mockResolvedValue({ succeeded: true });
  const { getByTestId, findByText } = await render(<ReportSickScreen />);

  await fireEvent.press(getByTestId("report-sick-cta"));
  await fireEvent.press(await waitFor(() => getByTestId("report-sick-confirm")));

  expect(await findByText("reportSick.success")).toBeTruthy();
});

it("disables the action and shows a message when offline (spec.md Offline behavior)", async () => {
  useIsOffline.mockReturnValue(true);
  const { getByTestId, findByText } = await render(<ReportSickScreen />);

  expect(await findByText("reportSick.needsConnection")).toBeTruthy();
  const btn = getByTestId("report-sick-cta");
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
});
