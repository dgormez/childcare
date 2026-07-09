import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { EditEventModal } from "../../components/EditEventModal";
import type { ChildEventResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/childEvents", () => ({ updateChildEvent: jest.fn() }));

const { updateChildEvent } = jest.requireMock("../../services/childEvents") as { updateChildEvent: jest.Mock };

function makeEvent(overrides: Partial<ChildEventResponse>): ChildEventResponse {
  return {
    id: "e1",
    childId: "child-1",
    eventType: "custom",
    occurredAt: new Date().toISOString(),
    endedAt: null,
    payload: { label: "Sunscreen applied" },
    visibleToParent: true,
    recordedBy: [],
    administeredBy: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides,
  };
}

beforeEach(() => {
  jest.clearAllMocks();
  updateChildEvent.mockResolvedValue(undefined);
});

// feature 009a FR-005/US1: a same-day `custom` event's label/text can be edited through the
// same generic field-by-field editor every other event type already uses (FR-006, feature 009).

it("shows the custom event's label as the modal title, and its label field is editable", async () => {
  const event = makeEvent({ payload: { label: "Sunscreen applied", text: "Reapplied after outdoor play" } });
  const { getByText, getByDisplayValue } = await render(
    <EditEventModal event={event} isConnected onClose={jest.fn()} onSaved={jest.fn()} />
  );

  expect(getByText("Sunscreen applied")).toBeTruthy();
  expect(getByDisplayValue("Sunscreen applied")).toBeTruthy();
  expect(getByDisplayValue("Reapplied after outdoor play")).toBeTruthy();
});

it("saves an edited custom label", async () => {
  const event = makeEvent({ payload: { label: "Sunscreen applied" } });
  const onSaved = jest.fn();
  const { getByDisplayValue, getByText } = await render(
    <EditEventModal event={event} isConnected onClose={jest.fn()} onSaved={onSaved} />
  );

  await act(async () => fireEvent.changeText(getByDisplayValue("Sunscreen applied"), "Sunscreen reapplied"));
  await act(async () => fireEvent.press(getByText("childEvents.save")));

  await waitFor(() =>
    expect(updateChildEvent).toHaveBeenCalledWith(
      "e1",
      { payload: { label: "Sunscreen reapplied" } },
      true
    )
  );
  expect(onSaved).toHaveBeenCalled();
});
