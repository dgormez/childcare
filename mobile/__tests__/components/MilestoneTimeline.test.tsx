import React from "react";
import { render } from "@testing-library/react-native";
import { MilestoneTimeline, type MilestoneTimelineEntry } from "../../components/milestones/MilestoneTimeline";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

function makeEntry(overrides: Partial<MilestoneTimelineEntry> = {}): MilestoneTimelineEntry {
  return {
    observationId: "obs-1",
    milestoneDescription: "Says first words",
    status: "achieved",
    observedAt: "2026-07-16",
    createdAt: "2026-07-16T00:00:00Z",
    ...overrides,
  };
}

it("shows a pending-sync badge for an observation recorded offline", async () => {
  const { getByText } = await render(<MilestoneTimeline entries={[makeEntry({ pending: true })]} />);
  expect(getByText("milestones.pendingSync")).toBeTruthy();
});

it("shows no pending-sync badge for an already-synced observation", async () => {
  const { queryByText } = await render(<MilestoneTimeline entries={[makeEntry({ pending: false })]} />);
  expect(queryByText("milestones.pendingSync")).toBeNull();
});

it("renders the most recently added entry first, matching caller-supplied order", async () => {
  const entries = [
    makeEntry({ observationId: "obs-2", milestoneDescription: "Newest observation" }),
    makeEntry({ observationId: "obs-1", milestoneDescription: "Older observation" }),
  ];
  const { getAllByText } = await render(<MilestoneTimeline entries={entries} />);
  const [first] = getAllByText(/observation$/);
  expect(first.props.children).toBe("Newest observation");
});
