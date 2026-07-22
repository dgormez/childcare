import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import LeaveRequestsScreen from "../app/(app)/leave-requests/index";
import NewLeaveRequestScreen from "../app/(app)/leave-requests/new";
import type { StaffLeaveRequestResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
jest.mock("../services/leaveRequests", () => ({
  getMyLeaveRequests: jest.fn(),
  submitLeaveRequest: jest.fn(),
}));
jest.mock("../hooks/useIsOffline", () => ({
  useIsOffline: jest.fn(() => false),
}));

const { getMyLeaveRequests, submitLeaveRequest } = require("../services/leaveRequests");
const { useIsOffline } = require("../hooks/useIsOffline");
const { useRouter } = require("expo-router");

beforeEach(() => {
  jest.clearAllMocks();
  useIsOffline.mockReturnValue(false);
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

const requests: StaffLeaveRequestResponse[] = [
  {
    id: "r1", staffProfileId: "s1", type: "annual", dateFrom: "2026-08-03", dateTo: "2026-08-07",
    notes: null, status: "pending", decidedBy: null, decidedAt: null, createdAt: "2026-07-22T09:00:00Z",
  },
];

describe("LeaveRequestsScreen (US4/FR-012)", () => {
  it("shows the empty state when there are no requests yet", async () => {
    getMyLeaveRequests.mockResolvedValue([]);
    const { findByText } = await render(<LeaveRequestsScreen />);
    expect(await findByText("leaveRequests.empty")).toBeTruthy();
  });

  it("shows a submitted request with its status", async () => {
    getMyLeaveRequests.mockResolvedValue(requests);
    const { findByText, findByTestId } = await render(<LeaveRequestsScreen />);
    await findByTestId("leave-request-r1");
    expect(await findByText("leaveRequests.type.annual")).toBeTruthy();
    expect(await findByText("leaveRequests.status.pending")).toBeTruthy();
  });
});

describe("NewLeaveRequestScreen (FR-009)", () => {
  it("submits the selected type and date range", async () => {
    submitLeaveRequest.mockResolvedValue({ succeeded: true });
    const back = jest.fn();
    useRouter.mockReturnValue({ back, push: jest.fn(), replace: jest.fn() });

    const { getByTestId } = await render(<NewLeaveRequestScreen />);
    await fireEvent.press(getByTestId("leave-type-sick"));
    await fireEvent.press(getByTestId("submit-leave-request"));

    await waitFor(() => expect(submitLeaveRequest).toHaveBeenCalled());
    await waitFor(() => expect(back).toHaveBeenCalled());
  });

  it("blocks submission while offline", async () => {
    useIsOffline.mockReturnValue(true);
    const { getByTestId, findByText } = await render(<NewLeaveRequestScreen />);

    expect(await findByText("leaveRequests.needsConnection")).toBeTruthy();
    const btn = getByTestId("submit-leave-request");
    expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
  });
});
