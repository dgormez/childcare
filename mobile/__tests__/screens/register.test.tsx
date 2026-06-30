import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import RegisterScreen from "../../app/(auth)/register";

jest.mock("../../services/auth", () => ({ register: jest.fn() }));
jest.mock("../../services/googleAuth", () => ({
  useGoogleSignIn: () => ({ signIn: jest.fn(), loading: false, error: "", ready: true, clearError: jest.fn() }),
}));

const { register } = require("../../services/auth");
const { useRouter }  = require("expo-router");

beforeEach(() => {
  jest.clearAllMocks();
  register.mockResolvedValue(undefined);
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

async function fillForm(utils: Awaited<ReturnType<typeof render>>, opts = { confirm: "password123" }) {
  await fireEvent.changeText(utils.getByPlaceholderText("you@example.com"),  "user@test.com");
  await fireEvent.changeText(utils.getByPlaceholderText("Min 8 characters"), "password123");
  await fireEvent.changeText(utils.getByPlaceholderText("••••••••"),          opts.confirm);
}

it("create button is disabled when fields are empty", async () => {
  const { getByText } = await render(<RegisterScreen />);
  const btn = getByText("Create account").parent!;
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
});

it("shows error when passwords do not match", async () => {
  const utils = await render(<RegisterScreen />);
  await fillForm(utils, { confirm: "different123" });
  await fireEvent.press(utils.getByText("Create account"));
  await waitFor(() => expect(utils.getByText("Passwords do not match.")).toBeTruthy());
});

it("calls register and redirects on success", async () => {
  const replace = jest.fn();
  useRouter.mockReturnValue({ replace, push: jest.fn(), back: jest.fn() });

  const utils = await render(<RegisterScreen />);
  await fillForm(utils);
  await fireEvent.press(utils.getByText("Create account"));

  await waitFor(() => expect(register).toHaveBeenCalledWith(expect.any(String), "user@test.com", "password123"));
  await waitFor(() => expect(replace).toHaveBeenCalledWith("/(tabs)"));
});

it("shows error modal when registration fails", async () => {
  register.mockRejectedValue(new Error("An account with this email already exists."));

  const utils = await render(<RegisterScreen />);
  await fillForm(utils);
  await fireEvent.press(utils.getByText("Create account"));

  await waitFor(() =>
    expect(utils.getByText("An account with this email already exists.")).toBeTruthy()
  );
});
