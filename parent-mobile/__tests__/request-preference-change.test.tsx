import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import RequestPreferenceChangeScreen from "../app/(app)/menu/request-preference-change";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/mealPreferenceRequests", () => ({
  submitMealPreferenceChangeRequest: jest.fn(),
}));

const { useRouter, useLocalSearchParams } = require("expo-router");
const { submitMealPreferenceChangeRequest } = jest.requireMock("../services/mealPreferenceRequests") as {
  submitMealPreferenceChangeRequest: jest.Mock;
};

beforeEach(() => {
  jest.clearAllMocks();
  useLocalSearchParams.mockReturnValue({ childId: "c1" });
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

it("submits the selected texture and dietary tags for the child from route params", async () => {
  submitMealPreferenceChangeRequest.mockResolvedValue({ id: "req-1", childId: "c1", status: "pending" });
  const back = jest.fn();
  useRouter.mockReturnValue({ back, push: jest.fn(), replace: jest.fn() });

  const { getByTestId, findByTestId } = await render(<RequestPreferenceChangeScreen />);

  await fireEvent.press(await findByTestId("texture-mixed"));
  await fireEvent.press(getByTestId("dietary-halal"));
  await act(async () => {
    fireEvent.changeText(getByTestId("notes-input"), "Kan nu goed kauwen.");
  });
  await fireEvent.press(getByTestId("submit-button"));

  await waitFor(() => expect(submitMealPreferenceChangeRequest).toHaveBeenCalledWith("c1", "mixed", ["halal"], "Kan nu goed kauwen."));
  await waitFor(() => expect(back).toHaveBeenCalled());
});

it("shows the inline duplicate-pending error on a 409 response, without navigating away", async () => {
  submitMealPreferenceChangeRequest.mockRejectedValue(new Error("errors.meal_preference_requests.duplicate_pending"));
  const back = jest.fn();
  useRouter.mockReturnValue({ back, push: jest.fn(), replace: jest.fn() });

  const { getByTestId, findByTestId, findByText } = await render(<RequestPreferenceChangeScreen />);

  await fireEvent.press(await findByTestId("texture-mixed"));
  await fireEvent.press(getByTestId("submit-button"));

  expect(await findByText("errors.meal_preference_requests.duplicate_pending")).toBeTruthy();
  expect(back).not.toHaveBeenCalled();
});

it("disables submit until at least one of texture/dietary type is chosen", async () => {
  const { getByTestId } = await render(<RequestPreferenceChangeScreen />);

  expect(getByTestId("submit-button").props.accessibilityState.disabled).toBe(true);
});
