import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { QuickActionSheet } from "../../components/QuickActionSheet";
import type { ChildEventResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/childEvents", () => ({
  recordChildEvent: jest.fn(),
  recordChildEventBatch: jest.fn(),
  endSleepEvent: jest.fn(),
}));

const mockUseNetworkStatus = jest.fn();
jest.mock("../../hooks/useNetworkStatus", () => ({ useNetworkStatus: () => mockUseNetworkStatus() }));

const { recordChildEvent, recordChildEventBatch } = jest.requireMock("../../services/childEvents") as {
  recordChildEvent: jest.Mock;
  recordChildEventBatch: jest.Mock;
};

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

// Feature 009c — User Stories 1 and 2: batch mode reuses this same sheet for multiple children
// at once (batchChildren prop), restricted to the eight multi-select-eligible event types.

const batchChildren = [
  { id: "c1", firstName: "Timmy", lastName: "Tester" },
  { id: "c2", firstName: "Amy", lastName: "Ainsworth" },
];

it("batch mode does not offer individual-only event types (temperature, medication, weight, growth_check)", async () => {
  const { queryByText } = await render(
    <QuickActionSheet
      visible childId="" batchChildren={batchChildren} inProgressSleepEventId={null}
      onClose={jest.fn()} onEventRecorded={jest.fn()}
    />
  );

  expect(queryByText("childEvents.types.temperature")).toBeNull();
  expect(queryByText("childEvents.types.medication")).toBeNull();
  expect(queryByText("childEvents.types.weight")).toBeNull();
  expect(queryByText("childEvents.types.growth_check")).toBeNull();
});

it("submitting a batch diaper event calls recordChildEventBatch with every selected child and shows a success toast via onBatchRecorded", async () => {
  recordChildEventBatch.mockResolvedValue({
    response: { created: [{ childId: "c1", eventId: "e1" }, { childId: "c2", eventId: "e2" }], errors: [] },
    itemIds: [],
  });
  const onBatchRecorded = jest.fn();
  const { getByText } = await render(
    <QuickActionSheet
      visible childId="" batchChildren={batchChildren} inProgressSleepEventId={null}
      onClose={jest.fn()} onEventRecorded={jest.fn()} onBatchRecorded={onBatchRecorded}
    />
  );

  fireEvent.press(getByText("childEvents.types.diaper"));
  await waitFor(() => expect(getByText("childEvents.diaper.wet")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("childEvents.diaper.wet")));

  await waitFor(() =>
    expect(recordChildEventBatch).toHaveBeenCalledWith(
      expect.objectContaining({ childIds: ["c1", "c2"], eventType: "diaper", payload: { type: "wet" } }),
      true
    )
  );
  await waitFor(() => expect(onBatchRecorded).toHaveBeenCalledWith({ createdCount: 2, failedCount: 0 }));
});

it("a partial batch failure shows the failed child's name and reason, and 'retry failed' resubmits only that child", async () => {
  recordChildEventBatch
    .mockResolvedValueOnce({
      response: { created: [{ childId: "c1", eventId: "e1" }], errors: [{ childId: "c2", reason: "not_present" }] },
      itemIds: [],
    })
    .mockResolvedValueOnce({ response: { created: [{ childId: "c2", eventId: "e2" }], errors: [] }, itemIds: [] });
  const onBatchRecorded = jest.fn();
  const { getByText } = await render(
    <QuickActionSheet
      visible childId="" batchChildren={batchChildren} inProgressSleepEventId={null}
      onClose={jest.fn()} onEventRecorded={jest.fn()} onBatchRecorded={onBatchRecorded}
    />
  );

  fireEvent.press(getByText("childEvents.types.note"));
  await waitFor(() => expect(getByText("childEvents.save")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("childEvents.save")));

  await waitFor(() => expect(getByText("Amy Ainsworth")).toBeTruthy());
  expect(getByText("childEvents.batch.reasons.not_present")).toBeTruthy();

  await act(async () => fireEvent.press(getByText("childEvents.batch.retryFailed")));

  expect(recordChildEventBatch).toHaveBeenCalledTimes(2);
  expect(recordChildEventBatch).toHaveBeenLastCalledWith(
    expect.objectContaining({ childIds: ["c2"], eventType: "note" }),
    true
  );
  await waitFor(() => expect(onBatchRecorded).toHaveBeenCalledWith({ createdCount: 2, failedCount: 0 }));
});
