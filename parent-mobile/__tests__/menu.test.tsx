import React from "react";
import { render } from "@testing-library/react-native";
import MenuScreen from "../app/(app)/menu/index";
import type { ParentMonthlyMenuEntry } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/menu", () => ({
  getMonthlyMenu: jest.fn(),
}));

const { getMonthlyMenu } = jest.requireMock("../services/menu") as { getMonthlyMenu: jest.Mock };

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
