import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import { ClockInOutCard } from "../components/ClockInOutCard";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
jest.mock("../services/timeEntries", () => ({
  clockIn: jest.fn(),
  clockOut: jest.fn(),
}));
jest.mock("../hooks/useIsOffline", () => ({
  useIsOffline: jest.fn(() => false),
}));
jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  return { apiClient: { GET: (...args: unknown[]) => mockGet(...args) }, __mockGet: mockGet };
});

const { clockIn, clockOut } = require("../services/timeEntries");
const { useIsOffline } = require("../hooks/useIsOffline");
const apiMock = jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock };
const getMock = apiMock.__mockGet;

const locationNamesById = new Map([
  ["loc-1", "Sunshine House"],
  ["loc-2", "Rainbow House"],
]);

function jsonResponse(body: unknown) {
  return { response: { ok: true }, data: body };
}

beforeEach(() => {
  jest.clearAllMocks();
  useIsOffline.mockReturnValue(false);
  getMock.mockResolvedValue(jsonResponse(null));
});

it("shows 'Begin dienst' when no entry is open, single eligible location, single function", async () => {
  const { findByTestId, findByText } = await render(
    <ClockInOutCard eligibleLocationIds={["loc-1"]} timeEntryFunctions={["kinderbegeleider"]} locationNamesById={locationNamesById} />,
  );

  expect(await findByText("timeEntries.clockInAction")).toBeTruthy();
  clockIn.mockResolvedValue({ succeeded: true, entry: { id: "e1", isOpen: true } });

  await fireEvent.press(await findByTestId("clock-in-out-cta"));
  await waitFor(() => expect(clockIn).toHaveBeenCalledWith("loc-1", null, null));
});

it("shows a location picker when more than one location is eligible", async () => {
  const { findByTestId, findByText, queryByText } = await render(
    <ClockInOutCard eligibleLocationIds={["loc-1", "loc-2"]} timeEntryFunctions={["kinderbegeleider"]} locationNamesById={locationNamesById} />,
  );

  await fireEvent.press(await findByTestId("clock-in-out-cta"));
  expect(await findByText("Sunshine House")).toBeTruthy();
  expect(queryByText("timeEntries.clockInAction")).toBeNull();

  clockIn.mockResolvedValue({ succeeded: true, entry: { id: "e1", isOpen: true } });
  await fireEvent.press(await findByText("Rainbow House"));
  await waitFor(() => expect(clockIn).toHaveBeenCalledWith("loc-2", null, null));
});

it("shows a function picker when more than one function is configured", async () => {
  const { findByTestId, findByText } = await render(
    <ClockInOutCard eligibleLocationIds={["loc-1"]} timeEntryFunctions={["kinderbegeleider", "logistiek"]} locationNamesById={locationNamesById} />,
  );

  await fireEvent.press(await findByTestId("clock-in-out-cta"));
  expect(await findByText("timeEntries.functions.kinderbegeleider")).toBeTruthy();

  clockIn.mockResolvedValue({ succeeded: true, entry: { id: "e1", isOpen: true } });
  await fireEvent.press(await findByText("timeEntries.functions.logistiek"));
  await waitFor(() => expect(clockIn).toHaveBeenCalledWith("loc-1", null, "logistiek"));
});

it("shows 'Einde dienst' when an entry is already open, and clocks out on press", async () => {
  getMock.mockResolvedValue(jsonResponse({ id: "e1", isOpen: true }));
  clockOut.mockResolvedValue({ succeeded: true, entry: { id: "e1", isOpen: false } });

  const { findByTestId, findByText } = await render(
    <ClockInOutCard eligibleLocationIds={["loc-1"]} timeEntryFunctions={["kinderbegeleider"]} locationNamesById={locationNamesById} />,
  );

  expect(await findByText("timeEntries.clockOutAction")).toBeTruthy();
  await fireEvent.press(await findByTestId("clock-in-out-cta"));
  await waitFor(() => expect(clockOut).toHaveBeenCalled());
});

it("disables the action and shows a message when offline", async () => {
  useIsOffline.mockReturnValue(true);
  const { findByTestId, findByText } = await render(
    <ClockInOutCard eligibleLocationIds={["loc-1"]} timeEntryFunctions={["kinderbegeleider"]} locationNamesById={locationNamesById} />,
  );

  expect(await findByText("timeEntries.needsConnection")).toBeTruthy();
  const btn = await findByTestId("clock-in-out-cta");
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
});
