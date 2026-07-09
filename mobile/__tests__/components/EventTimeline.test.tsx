import React from "react";
import { render } from "@testing-library/react-native";
import { EventTimeline } from "../../components/EventTimeline";
import type { ChildEventResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

function makeEvent(overrides: Partial<ChildEventResponse>): ChildEventResponse {
  return {
    id: "e1",
    childId: "child-1",
    eventType: "note",
    occurredAt: new Date().toISOString(),
    endedAt: null,
    payload: {},
    visibleToParent: true,
    recordedBy: [],
    administeredBy: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides,
  };
}

// feature 009a FR-004: a `custom` event renders its label as the headline (not the generic
// type name every other event uses), with `text` as secondary detail beneath it.

it("renders a custom event's label as its headline", async () => {
  const event = makeEvent({ eventType: "custom", payload: { label: "Sunscreen applied" } });
  const { getByText, queryByText } = await render(
    <EventTimeline events={[event]} onEdit={jest.fn()} onDelete={jest.fn()} />
  );

  expect(getByText("Sunscreen applied")).toBeTruthy();
  expect(queryByText("childEvents.types.custom")).toBeNull();
});

it("renders a custom event's optional text as secondary detail beneath the label", async () => {
  const event = makeEvent({
    eventType: "custom",
    payload: { label: "Sunscreen applied", text: "Reapplied after outdoor play" },
  });
  const { getByText } = await render(<EventTimeline events={[event]} onEdit={jest.fn()} onDelete={jest.fn()} />);

  expect(getByText("Sunscreen applied")).toBeTruthy();
  expect(getByText("Reapplied after outdoor play")).toBeTruthy();
});

it("renders every other event type's generic type name unchanged", async () => {
  const event = makeEvent({ eventType: "growth_check", payload: { weightKg: 9.2 } });
  const { getByText } = await render(<EventTimeline events={[event]} onEdit={jest.fn()} onDelete={jest.fn()} />);

  expect(getByText("childEvents.types.growth_check")).toBeTruthy();
});
