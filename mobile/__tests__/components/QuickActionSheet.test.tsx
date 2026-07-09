import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { QuickActionSheet } from "../../components/QuickActionSheet";
import type { ChildEventResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/childEvents", () => ({
  recordChildEvent: jest.fn(),
  endSleepEvent: jest.fn(),
}));

const mockUseNetworkStatus = jest.fn();
jest.mock("../../hooks/useNetworkStatus", () => ({ useNetworkStatus: () => mockUseNetworkStatus() }));

const { recordChildEvent } = jest.requireMock("../../services/childEvents") as { recordChildEvent: jest.Mock };

const fakeEvent = { id: "e1", childId: "child-1" } as ChildEventResponse;

beforeEach(() => {
  jest.clearAllMocks();
  mockUseNetworkStatus.mockReturnValue({ isConnected: true });
  recordChildEvent.mockResolvedValue(fakeEvent);
});

// FR-021/SC-001: routine event types (diaper, mood, feeding_bottle) resolve in exactly 2 taps
// after the sheet is already open — select type, then select value. No intermediate screens,
// no typing.

it("records a diaper event in 2 taps: select 'diaper', then select 'wet'", async () => {
  const onEventRecorded = jest.fn();
  const { getByText } = await render(
    <QuickActionSheet visible childId="child-1" inProgressSleepEventId={null} onClose={jest.fn()} onEventRecorded={onEventRecorded} />
  );

  fireEvent.press(getByText("childEvents.types.diaper")); // tap 1
  await waitFor(() => expect(getByText("childEvents.diaper.wet")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("childEvents.diaper.wet"))); // tap 2

  await waitFor(() =>
    expect(recordChildEvent).toHaveBeenCalledWith(
      expect.objectContaining({ childId: "child-1", eventType: "diaper", payload: { type: "wet" } }),
      true
    )
  );
  await waitFor(() => expect(onEventRecorded).toHaveBeenCalledWith(fakeEvent));
});

it("records a mood event in 2 taps: select 'mood', then select 'great'", async () => {
  const { getByText } = await render(
    <QuickActionSheet visible childId="child-1" inProgressSleepEventId={null} onClose={jest.fn()} onEventRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("childEvents.types.mood"));
  await waitFor(() => expect(getByText("childEvents.mood.great")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("childEvents.mood.great")));

  await waitFor(() =>
    expect(recordChildEvent).toHaveBeenCalledWith(
      expect.objectContaining({ eventType: "mood", payload: { value: "great" } }),
      true
    )
  );
});

it("records a bottle-feeding event in 2 taps: select 'feeding_bottle', then select a preset amount", async () => {
  const { getByText } = await render(
    <QuickActionSheet visible childId="child-1" inProgressSleepEventId={null} onClose={jest.fn()} onEventRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("childEvents.types.feeding_bottle"));
  await waitFor(() => expect(getByText("120")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("120")));

  await waitFor(() =>
    expect(recordChildEvent).toHaveBeenCalledWith(
      expect.objectContaining({ eventType: "feeding_bottle", payload: { ml: 120 } }),
      true
    )
  );
});

it("starting a nap (no in-progress sleep) is a single tap with no fields", async () => {
  const { getByText } = await render(
    <QuickActionSheet visible childId="child-1" inProgressSleepEventId={null} onClose={jest.fn()} onEventRecorded={jest.fn()} />
  );

  await act(async () => fireEvent.press(getByText("childEvents.types.sleep")));

  await waitFor(() =>
    expect(recordChildEvent).toHaveBeenCalledWith(expect.objectContaining({ eventType: "sleep", payload: {} }), true)
  );
});

// feature 009a: `custom` event — free-text label (required) + optional detail text, no
// autocomplete/suggestion of prior labels (2026-07-09 clarification).

it("blocks saving a custom event with no label entered", async () => {
  const { getByText, queryByText } = await render(
    <QuickActionSheet visible childId="child-1" inProgressSleepEventId={null} onClose={jest.fn()} onEventRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("childEvents.types.custom"));
  await waitFor(() => expect(queryByText("childEvents.custom.label")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("childEvents.save")));

  expect(recordChildEvent).not.toHaveBeenCalled();
});

it("records a custom event with a label only", async () => {
  const { getByText, getByPlaceholderText } = await render(
    <QuickActionSheet visible childId="child-1" inProgressSleepEventId={null} onClose={jest.fn()} onEventRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("childEvents.types.custom"));
  await waitFor(() => expect(getByPlaceholderText("childEvents.custom.labelPlaceholder")).toBeTruthy());
  await act(async () => fireEvent.changeText(getByPlaceholderText("childEvents.custom.labelPlaceholder"), "Sunscreen applied"));
  await act(async () => fireEvent.press(getByText("childEvents.save")));

  await waitFor(() =>
    expect(recordChildEvent).toHaveBeenCalledWith(
      expect.objectContaining({ eventType: "custom", payload: { label: "Sunscreen applied" } }),
      true
    )
  );
});
