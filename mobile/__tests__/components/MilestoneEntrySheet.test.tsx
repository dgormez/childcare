import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { MilestoneEntrySheet } from "../../components/milestones/MilestoneEntrySheet";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: "en" } }),
}));

jest.mock("../../services/milestones", () => ({
  fetchDevelopmentalDomains: jest.fn(),
  recordMilestoneObservation: jest.fn(),
}));

const { fetchDevelopmentalDomains, recordMilestoneObservation } = jest.requireMock("../../services/milestones") as {
  fetchDevelopmentalDomains: jest.Mock;
  recordMilestoneObservation: jest.Mock;
};

const domains = [
  {
    id: "domain-1",
    code: "language",
    nameNl: "Taal",
    nameFr: "Langage",
    nameEn: "Language",
    sortOrder: 1,
    milestones: [
      {
        id: "milestone-1",
        ageFromMonths: 12,
        ageToMonths: 18,
        descriptionNl: "Nl beschrijving",
        descriptionFr: "Fr description",
        descriptionEn: "Says first words",
        sortOrder: 1,
        currentStatus: null,
        isCurrentFocus: true,
        history: null,
      },
    ],
  },
];

beforeEach(() => {
  jest.clearAllMocks();
  fetchDevelopmentalDomains.mockResolvedValue(domains);
});

it("recording an observation walks domain -> milestone -> status and calls the API", async () => {
  const observation = { id: "obs-1", status: "achieved", observedAt: "2026-07-16", notes: null, createdAt: "2026-07-16T00:00:00Z" };
  recordMilestoneObservation.mockResolvedValue(observation);
  const onSaved = jest.fn();
  const onClose = jest.fn();

  const { findByText, getByText } = await render(
    <MilestoneEntrySheet visible childId="child-1" isConnected onClose={onClose} onSaved={onSaved} />
  );

  await findByText("Language");
  await act(async () => fireEvent.press(getByText("Language")));

  await findByText("Says first words");
  await act(async () => fireEvent.press(getByText("Says first words")));

  await findByText("milestones.status.achieved");
  await act(async () => fireEvent.press(getByText("milestones.status.achieved")));
  await act(async () => fireEvent.press(getByText("milestones.save")));

  await waitFor(() =>
    expect(recordMilestoneObservation).toHaveBeenCalledWith(
      expect.objectContaining({ childId: "child-1", milestoneId: "milestone-1", status: "achieved" }),
      true
    )
  );
  expect(onSaved).toHaveBeenCalledWith(observation, "Says first words", false);
  expect(onClose).toHaveBeenCalled();
});

it("shows the current-focus badge for an age-appropriate milestone", async () => {
  const { findByText, getByText } = await render(
    <MilestoneEntrySheet visible childId="child-1" isConnected onClose={jest.fn()} onSaved={jest.fn()} />
  );

  await findByText("Language");
  await act(async () => fireEvent.press(getByText("Language")));

  expect(await findByText("milestones.currentFocus")).toBeTruthy();
});

it("recording while offline queues locally and reports the observation as pending", async () => {
  const observation = { id: "local-1", status: "emerging", observedAt: "2026-07-16", notes: null, createdAt: "2026-07-16T00:00:00Z" };
  recordMilestoneObservation.mockResolvedValue(observation);
  const onSaved = jest.fn();

  const { findByText, getByText } = await render(
    <MilestoneEntrySheet visible childId="child-1" isConnected={false} onClose={jest.fn()} onSaved={onSaved} />
  );

  await findByText("Language");
  await act(async () => fireEvent.press(getByText("Language")));
  await findByText("Says first words");
  await act(async () => fireEvent.press(getByText("Says first words")));
  await findByText("milestones.status.emerging");
  await act(async () => fireEvent.press(getByText("milestones.status.emerging")));
  await act(async () => fireEvent.press(getByText("milestones.save")));

  await waitFor(() =>
    expect(recordMilestoneObservation).toHaveBeenCalledWith(expect.objectContaining({ status: "emerging" }), false)
  );
  expect(onSaved).toHaveBeenCalledWith(observation, "Says first words", true);
});
