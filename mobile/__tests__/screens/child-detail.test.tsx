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

const { useLocalSearchParams } = require("expo-router");
const { getCached } = require("../../services/readCache");

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
