import React from "react";
import { render } from "@testing-library/react-native";
import MenuScreen from "../app/(app)/menu/index";
import type { ParentMonthlyMenuEntry, ParentChildResponse, ParentMealPreferenceResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/menu", () => ({
  getMonthlyMenu: jest.fn(),
}));

jest.mock("../services/mealPreferenceRequests", () => ({
  getMealPreference: jest.fn(),
}));

jest.mock("../services/apiClient", () => ({
  apiClient: { GET: jest.fn().mockResolvedValue({ response: { ok: true }, data: [] }) },
}));

const { getMonthlyMenu } = jest.requireMock("../services/menu") as { getMonthlyMenu: jest.Mock };
const { getMealPreference } = jest.requireMock("../services/mealPreferenceRequests") as { getMealPreference: jest.Mock };
const { apiClient } = jest.requireMock("../services/apiClient") as { apiClient: { GET: jest.Mock } };

const child1: ParentChildResponse = { id: "c1", firstName: "Timmy", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2022-01-01" };

function todayIso(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

beforeEach(() => {
  jest.clearAllMocks();
});

it("renders a published menu's day entries", async () => {
  const entries: ParentMonthlyMenuEntry[] = [
    {
      locationId: "loc-1",
      locationName: "KDV Zonnebloem",
      isPublished: true,
      days: [{ date: todayIso(), soup: "Tomatensoep", mainCourse: "Kip met puree", dessert: "Yoghurt", notes: null }],
      closureDates: [],
    },
  ];
  getMonthlyMenu.mockResolvedValue({ status: "loaded", entries });

  const { findByText } = await render(<MenuScreen />);

  expect(await findByText("KDV Zonnebloem")).toBeTruthy();
  expect(await findByText("Tomatensoep · Kip met puree · Yoghurt")).toBeTruthy();
});

it("shows the not-available placeholder for a location with no published menu", async () => {
  const entries: ParentMonthlyMenuEntry[] = [
    { locationId: "loc-1", locationName: "KDV Zonnebloem", isPublished: false, days: [], closureDates: [] },
  ];
  getMonthlyMenu.mockResolvedValue({ status: "loaded", entries });

  const { findByText } = await render(<MenuScreen />);

  expect(await findByText("menu.notAvailable")).toBeTruthy();
});

it("renders a day with no entries as a dash, not blank", async () => {
  const entries: ParentMonthlyMenuEntry[] = [
    { locationId: "loc-1", locationName: "KDV Zonnebloem", isPublished: true, days: [], closureDates: [] },
  ];
  getMonthlyMenu.mockResolvedValue({ status: "loaded", entries });

  const { findAllByText } = await render(<MenuScreen />);

  expect((await findAllByText("—")).length).toBeGreaterThan(0);
});

it("labels a closure day distinctly, not just by color", async () => {
  const entries: ParentMonthlyMenuEntry[] = [
    { locationId: "loc-1", locationName: "KDV Zonnebloem", isPublished: true, days: [], closureDates: [todayIso()] },
  ];
  getMonthlyMenu.mockResolvedValue({ status: "loaded", entries });

  const { findByText } = await render(<MenuScreen />);

  expect(await findByText("menu.closed")).toBeTruthy();
});

it("shows a load-failed state when the service reports unavailable", async () => {
  getMonthlyMenu.mockResolvedValue({ status: "unavailable" });

  const { findByText } = await render(<MenuScreen />);

  expect(await findByText("menu.loadFailed")).toBeTruthy();
});

it("shows both texture and dietary tags in the child's preference indicator, not texture alone (FR-010)", async () => {
  getMonthlyMenu.mockResolvedValue({ status: "loaded", entries: [] });
  apiClient.GET.mockImplementation((path: string) =>
    path === "/api/parent/children"
      ? Promise.resolve({ response: { ok: true }, data: [child1] })
      : Promise.resolve({ response: { ok: true }, data: [] }),
  );
  const preference: ParentMealPreferenceResponse = { texture: "mixed", dietaryType: ["halal"], hasPendingRequest: false };
  getMealPreference.mockResolvedValue(preference);

  const { findByText } = await render(<MenuScreen />);

  expect(await findByText("mealPreferenceRequests.texture.mixed · mealPreferenceRequests.dietaryType.halal")).toBeTruthy();
});
