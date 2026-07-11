import React from "react";
import { render, act } from "@testing-library/react-native";
import HomeScreen from "../app/(app)/index";
import type { DailySummaryResponse, ParentChildResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args) },
    configureApiBaseUrl: jest.fn(),
    __mockGet: mockGet,
  };
});

const getMock = (jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock }).__mockGet;

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

const child1: ParentChildResponse = { id: "c1", firstName: "Timmy", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2022-01-01" };
const child2: ParentChildResponse = { id: "c2", firstName: "Anna", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2023-01-01" };

const busySummary: DailySummaryResponse = {
  napsCount: 2, bottlesCount: 3, diaperChangesCount: 4,
  latestMood: "happy", latestTemperatureCelsius: 37.1,
  medicationAdministered: true, activities: ["Painting"], groupActivities: [],
};

const emptySummary: DailySummaryResponse = {
  napsCount: 0, bottlesCount: 0, diaperChangesCount: 0,
  latestMood: null, latestTemperatureCelsius: null,
  medicationAdministered: false, activities: [], groupActivities: [],
};

beforeEach(() => {
  jest.clearAllMocks();
});

it("renders two children's summaries clearly separated, each in its own card", async () => {
  getMock.mockImplementation((path: string, opts?: { params?: { path?: { childId?: string } } }) => {
    if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1, child2]));
    if (path === "/api/parent/children/{childId}/daily-summary") {
      const childId = opts?.params?.path?.childId;
      return Promise.resolve(jsonResponse(200, childId === "c1" ? busySummary : emptySummary));
    }
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByTestId } = await render(<HomeScreen />);

  const card1 = await findByTestId("daily-summary-card-c1");
  const card2 = await findByTestId("daily-summary-card-c2");
  expect(card1).toBeTruthy();
  expect(card2).toBeTruthy();
});

it("shows a 'no updates yet' empty state for a child with zero events today", async () => {
  getMock.mockImplementation((path: string) => {
    if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child2]));
    if (path === "/api/parent/children/{childId}/daily-summary") return Promise.resolve(jsonResponse(200, emptySummary));
    return Promise.resolve(jsonResponse(404, {}));
  });

  const { findByText } = await render(<HomeScreen />);

  expect(await findByText("home.noUpdatesYet")).toBeTruthy();
});

// feature 009b: a new "Activiteiten" section within this existing card (spec.md Assumptions —
// no merged event/activity timeline exists on the parent side, unlike the caregiver tablet).
describe("group activities section (feature 009b)", () => {
  const summaryWithConsentedPhotos: DailySummaryResponse = {
    ...emptySummary,
    groupActivities: [{
      id: "activity-1", activityType: "outdoor", title: "In de tuin", description: "Buiten gespeeld",
      occurredAt: "2026-07-11T09:00:00.000Z",
      photos: [{ id: "photo-1", downloadUrl: "https://example.test/photo.jpg", thumbnailDownloadUrl: "https://example.test/thumb.jpg", caption: null, uploadedAt: "2026-07-11T09:05:00.000Z" }],
    }],
  };

  const summaryWithoutConsentPhotos: DailySummaryResponse = {
    ...emptySummary,
    groupActivities: [{
      id: "activity-2", activityType: "story", title: "Verhaaltje", description: null,
      occurredAt: "2026-07-11T09:00:00.000Z", photos: [],
    }],
  };

  it("shows title/description and photos when the child has photos_internal consent", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      if (path === "/api/parent/children/{childId}/daily-summary") return Promise.resolve(jsonResponse(200, summaryWithConsentedPhotos));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText, findAllByLabelText } = await render(<HomeScreen />);

    expect(await findByText("In de tuin")).toBeTruthy();
    expect(await findByText("Buiten gespeeld")).toBeTruthy();
    expect(await findAllByLabelText("In de tuin")).toHaveLength(1);
  });

  it("still shows title/description with no photos when the child has no photo consent", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      if (path === "/api/parent/children/{childId}/daily-summary") return Promise.resolve(jsonResponse(200, summaryWithoutConsentPhotos));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText, queryAllByLabelText } = await render(<HomeScreen />);

    expect(await findByText("Verhaaltje")).toBeTruthy();
    expect(queryAllByLabelText("Verhaaltje")).toHaveLength(0);
  });
});
