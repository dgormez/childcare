import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import HomeScreen from "../app/(app)/index";
import AbsenceRequestScreen from "../app/(app)/requests/absence";
import type { DailySummaryResponse, ParentChildResponse, ReservationAvailabilityResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts ? `${key}:${JSON.stringify(opts)}` : key) }),
}));

jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  const mockPost = jest.fn();
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args), POST: (...args: unknown[]) => mockPost(...args) },
    configureApiBaseUrl: jest.fn(),
    __mockGet: mockGet,
    __mockPost: mockPost,
  };
});

const apiMock = jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock; __mockPost: jest.Mock };
const getMock = apiMock.__mockGet;
const { useRouter } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

const child1: ParentChildResponse = { id: "c1", firstName: "Timmy", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2022-01-01" };

const emptySummary: DailySummaryResponse = {
  napsCount: 0, bottlesCount: 0, diaperChangesCount: 0,
  latestMood: null, latestTemperatureCelsius: null,
  medicationAdministered: false, activities: [], groupActivities: [],
};

function availability(overrides: Partial<ReservationAvailabilityResponse> = {}): ReservationAvailabilityResponse {
  return { absence: "approval", extra: "approval", exchange: "disabled", noticeHours: 0, ...overrides };
}

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

describe("HomeScreen quick actions (feature 013f, research.md R6)", () => {
  it("hides the 'report sick' quick action when absence is disabled for the parent's only child", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      if (path === "/api/parent/children/{childId}/daily-summary") return Promise.resolve(jsonResponse(200, emptySummary));
      if (path === "/api/parent/children/{childId}/reservation-availability") return Promise.resolve(jsonResponse(200, availability({ absence: "disabled" })));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText, queryByText } = await render(<HomeScreen />);

    await findByText("home.quickActions.title");
    await waitFor(() => expect(queryByText("home.quickActions.reportSick")).toBeNull());
    // Exchange defaults to "disabled" too, per the fixture — extra stays available.
    expect(await findByText("home.quickActions.requestExtra")).toBeTruthy();
  });

  it("still shows a quick action when at least one linked child allows it", async () => {
    const child2: ParentChildResponse = { id: "c2", firstName: "Anna", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2023-01-01" };
    getMock.mockImplementation((path: string, opts?: { params?: { path?: { childId?: string } } }) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1, child2]));
      if (path === "/api/parent/children/{childId}/daily-summary") return Promise.resolve(jsonResponse(200, emptySummary));
      if (path === "/api/parent/children/{childId}/reservation-availability") {
        const childId = opts?.params?.path?.childId;
        return Promise.resolve(jsonResponse(200, availability({ absence: childId === "c1" ? "disabled" : "approval" })));
      }
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText } = await render(<HomeScreen />);

    expect(await findByText("home.quickActions.reportSick")).toBeTruthy();
  });
});

describe("DayReservationForm per-child block (feature 013f FR-006)", () => {
  it("blocks submission and shows an inline message when the type is disabled for the selected child", async () => {
    getMock.mockImplementation((path: string) => {
      if (path === "/api/parent/children") return Promise.resolve(jsonResponse(200, [child1]));
      if (path === "/api/parent/children/{childId}/reservation-availability") return Promise.resolve(jsonResponse(200, availability({ absence: "disabled" })));
      return Promise.resolve(jsonResponse(404, {}));
    });

    const { findByText, getByPlaceholderText } = await render(<AbsenceRequestScreen />);

    await findByText("Timmy Tester");
    expect(await findByText(/dayReservations.notAvailableForChild/)).toBeTruthy();

    await fireEvent.changeText(getByPlaceholderText("dayReservations.chooseDate"), "2026-07-13");
    const submitButton = await findByText("dayReservations.submit");
    await fireEvent.press(submitButton);

    expect(apiMock.__mockPost).not.toHaveBeenCalled();
  });
});
