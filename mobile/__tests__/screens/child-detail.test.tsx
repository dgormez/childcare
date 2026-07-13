import React from "react";
import { render } from "@testing-library/react-native";
import ChildDetailScreen from "../../app/(app)/child/[id]";
import type { ChildResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/readCache", () => ({
  getCached: jest.fn(),
}));

jest.mock("../../services/childEvents", () => ({
  listChildEvents: jest.fn().mockResolvedValue({ items: [], nextCursor: null }),
  recordChildEvent: jest.fn(),
  updateChildEvent: jest.fn(),
  deleteChildEvent: jest.fn(),
  endSleepEvent: jest.fn(),
}));

jest.mock("../../services/offlineQueue", () => ({
  getPending: jest.fn().mockResolvedValue([]),
  enqueue: jest.fn(),
}));

jest.mock("../../services/healthSummary", () => ({
  getChildHealthSummary: jest.fn().mockResolvedValue({ status: "unavailable" }),
}));

const { useLocalSearchParams } = require("expo-router");
const { getCached } = require("../../services/readCache");
const { getChildHealthSummary } = require("../../services/healthSummary");

const child: ChildResponse = {
  id: "c1", firstName: "Timmy", lastName: "Tester",
  dateOfBirth: "2022-01-01", photoDownloadUrl: null,
  allergiesDescription: "Peanuts", allergySeverity: "severe",
  medicalConditions: "Asthma", dietaryRestrictions: "Lactose-free",
  deactivatedAt: null,
};

beforeEach(() => {
  jest.clearAllMocks();
  useLocalSearchParams.mockReturnValue({ id: "c1" });
});

it("renders the tapped child's allergy, medical-conditions, and dietary-restrictions content from the cache (FR-008)", async () => {
  getCached.mockReturnValue([child]);

  const { getByText } = await render(<ChildDetailScreen />);

  expect(getByText("Timmy Tester")).toBeTruthy();
  expect(getByText("child.allergyAlert")).toBeTruthy();
  expect(getByText("Peanuts")).toBeTruthy();
  expect(getByText("severe")).toBeTruthy();
  expect(getByText("child.medicalConditions")).toBeTruthy();
  expect(getByText("Asthma")).toBeTruthy();
  expect(getByText("child.dietaryRestrictions")).toBeTruthy();
  expect(getByText("Lactose-free")).toBeTruthy();
});

it("omits sections for fields the child doesn't have", async () => {
  getCached.mockReturnValue([{ ...child, allergiesDescription: null, allergySeverity: null, dietaryRestrictions: null }]);

  const { queryByText, getByText } = await render(<ChildDetailScreen />);

  expect(getByText("child.medicalConditions")).toBeTruthy();
  expect(queryByText("child.allergyAlert")).toBeNull();
  expect(queryByText("child.dietaryRestrictions")).toBeNull();
});

it("shows an empty state when the child isn't found in the cache", async () => {
  getCached.mockReturnValue([]);
  useLocalSearchParams.mockReturnValue({ id: "nonexistent" });

  const { getByText } = await render(<ChildDetailScreen />);

  expect(getByText("groupView.empty")).toBeTruthy();
});

describe("health summary (feature 013c, US4)", () => {
  it("shows active health records and due-soon/overdue vaccine flags", async () => {
    getCached.mockReturnValue([child]);
    getChildHealthSummary.mockResolvedValue({
      status: "loaded",
      summary: {
        childId: "c1",
        activeHealthRecords: [
          { id: "h1", childId: "c1", recordType: "allergy", title: "Peanut allergy", description: "Confirmed by allergist.",
            validFrom: null, validUntil: null, isExpired: false, attachmentDownloadUrl: null, recordedBy: null,
            createdAt: "2026-01-01T00:00:00Z", updatedAt: null },
        ],
        dueSoonVaccines: [
          { vaccineName: "DTP", nextDueDate: "2026-07-20", isOverdue: false },
          { vaccineName: "Hep B", nextDueDate: "2026-07-01", isOverdue: true },
        ],
      },
    });

    const { findByText } = await render(<ChildDetailScreen />);

    expect(await findByText("Peanut allergy")).toBeTruthy();
    expect(await findByText("Confirmed by allergist.")).toBeTruthy();
    expect(await findByText("DTP")).toBeTruthy();
    expect(await findByText("Hep B")).toBeTruthy();
  });

  it("shows a calm empty state when there are no health records or vaccine flags", async () => {
    getCached.mockReturnValue([child]);
    getChildHealthSummary.mockResolvedValue({
      status: "loaded",
      summary: { childId: "c1", activeHealthRecords: [], dueSoonVaccines: [] },
    });

    const { findByText } = await render(<ChildDetailScreen />);

    expect(await findByText("child.healthSummary.empty")).toBeTruthy();
  });

  it("shows a distinct message when the summary can't load and nothing is cached", async () => {
    getCached.mockReturnValue([child]);
    getChildHealthSummary.mockResolvedValue({ status: "unavailable" });

    const { findByText } = await render(<ChildDetailScreen />);

    expect(await findByText("child.healthSummary.unavailable")).toBeTruthy();
  });
});
