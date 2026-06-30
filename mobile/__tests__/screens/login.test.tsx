import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import LoginScreen from "../../app/(auth)/login";

jest.mock("../../services/auth", () => ({ login: jest.fn() }));
jest.mock("../../services/googleAuth", () => ({
  useGoogleSignIn: () => ({ signIn: jest.fn(), loading: false, error: "", ready: true, clearError: jest.fn() }),
}));

const { login } = require("../../services/auth");
const { useRouter } = require("expo-router");

beforeEach(() => {
  jest.clearAllMocks();
  login.mockResolvedValue(undefined);
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

it("sign-in button is disabled when fields are empty", async () => {
  const { getByText } = await render(<LoginScreen />);
  const btn = getByText("Sign in").parent!;
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
});

it("sign-in button enables when email and password are filled", async () => {
  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "user@test.com");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "password123");
  const btn = getByText("Sign in").parent!;
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeFalsy();
});

it("calls login with trimmed email and password", async () => {
  const replace = jest.fn();
  useRouter.mockReturnValue({ replace, push: jest.fn(), back: jest.fn() });

  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "  user@test.com  ");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "password123");
  await fireEvent.press(getByText("Sign in"));

  await waitFor(() => expect(login).toHaveBeenCalledWith(expect.any(String), "user@test.com", "password123"));
  await waitFor(() => expect(replace).toHaveBeenCalledWith("/(tabs)"));
});

it("shows error modal when login fails", async () => {
  login.mockRejectedValue(new Error("Invalid email or password."));

  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "user@test.com");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "wrongpass");
  await fireEvent.press(getByText("Sign in"));

  await waitFor(() => expect(getByText("Invalid email or password.")).toBeTruthy());
});
