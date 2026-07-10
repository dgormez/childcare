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
  medicationAdministered: true, activities: ["Painting"],
};

const emptySummary: DailySummaryResponse = {
  napsCount: 0, bottlesCount: 0, diaperChangesCount: 0,
  latestMood: null, latestTemperatureCelsius: null,
  medicationAdministered: false, activities: [],
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
