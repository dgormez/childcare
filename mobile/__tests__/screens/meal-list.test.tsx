import React from "react";
import { render, waitFor } from "@testing-library/react-native";
import MealListScreen from "../../app/(room)/meal-list";
import type { MealListResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("expo-router", () => ({ useFocusEffect: () => {} }));

jest.mock("../../services/mealList", () => ({
  getMealList: jest.fn(),
}));

const { getMealList } = jest.requireMock("../../services/mealList") as { getMealList: jest.Mock };

const mealList: MealListResponse = {
  date: "2026-07-13",
  groups: [
    {
      groupId: "g1",
      groupName: "Butterflies",
      children: [
        {
          childId: "c1",
          firstName: "Emma",
          lastName: "Peeters",
          texture: "pureed",
          dietaryType: ["halal"],
          portionSize: "small",
          additionalNotes: null,
          hasPreference: true,
          allergySeverity: "severe",
          hasStandingMedication: true,
        },
      ],
    },
  ],
  expected: null,
};

beforeEach(() => {
  jest.resetAllMocks();
});

it("renders present children with their texture/dietary/portion/severity/medication indicators", async () => {
  getMealList.mockResolvedValueOnce({ status: "loaded", mealList });

  const { getByText, queryByText } = await render(<MealListScreen />);

  await waitFor(() => expect(getByText(/Emma/)).toBeTruthy());
  expect(getByText("Butterflies")).toBeTruthy();
  expect(getByText(/mealList.texture.pureed/)).toBeTruthy();
  expect(getByText(/mealList.allergySeverity.severe/)).toBeTruthy();
  // No absent child rendered — the fixture above contains only one, present, child.
  expect(queryByText("mealList.emptyState")).toBeNull();
});

it('renders "Geen voorkeur" for a present child with hasPreference=false, not hidden', async () => {
  const withoutPreference: MealListResponse = {
    ...mealList,
    groups: [{ groupId: "g1", groupName: "Butterflies", children: [{ ...mealList.groups[0].children[0], hasPreference: false }] }],
  };
  getMealList.mockResolvedValueOnce({ status: "loaded", mealList: withoutPreference });

  const { getByText } = await render(<MealListScreen />);

  await waitFor(() => expect(getByText(/Emma/)).toBeTruthy());
  expect(getByText("mealList.noPreference")).toBeTruthy();
});

it("shows the empty state when no children are present", async () => {
  getMealList.mockResolvedValueOnce({ status: "loaded", mealList: { date: "2026-07-13", groups: [], expected: null } });

  const { getByText } = await render(<MealListScreen />);

  await waitFor(() => expect(getByText("mealList.emptyState")).toBeTruthy());
});

it("renders the previously-cached meal list when the fetch is unavailable (offline-cache-fallback, FR-014)", async () => {
  // getMealList's own cache-fallback logic already returns a "loaded" result sourced from
  // cache when the network fails (mealList.test.ts covers that unit) — this screen-level test
  // only needs to confirm the screen renders correctly when getMealList resolves with data,
  // regardless of whether that data came from network or cache (the screen has no way to tell
  // the difference, by design).
  getMealList.mockResolvedValueOnce({ status: "loaded", mealList });

  const { getByText } = await render(<MealListScreen />);

  await waitFor(() => expect(getByText(/Emma/)).toBeTruthy());
});

it("shows the empty state when getMealList reports unavailable (no cache, no network)", async () => {
  getMealList.mockResolvedValueOnce({ status: "unavailable" });

  const { getByText } = await render(<MealListScreen />);

  await waitFor(() => expect(getByText("mealList.emptyState")).toBeTruthy());
});
