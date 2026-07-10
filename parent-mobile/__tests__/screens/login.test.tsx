import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import LoginScreen from "../../app/(auth)/login";

jest.mock("../../services/auth", () => ({
  login: jest.fn(),
  loginWithGoogle: jest.fn(),
  loginWithApple: jest.fn(),
}));
jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
jest.mock("expo-web-browser", () => ({ maybeCompleteAuthSession: jest.fn() }));
jest.mock("expo-auth-session/providers/google", () => ({
  useAuthRequest: jest.fn(() => [null, null, jest.fn()]),
}));
jest.mock("expo-apple-authentication", () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(false),
  signInAsync: jest.fn(),
  AppleAuthenticationScope: { FULL_NAME: 0, EMAIL: 1 },
}));
jest.mock("expo-constants", () => ({
  expoConfig: {
    extra: {
      googleIosClientId: "YOUR_GOOGLE_IOS_CLIENT_ID",
      googleWebClientId: "YOUR_GOOGLE_WEB_CLIENT_ID",
    },
  },
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
  const btn = getByText("login.submit").parent!;
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
});

it("sign-in button enables when organisation, email, and password are filled", async () => {
  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("acme-kdv"), "org-a");
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "user@test.com");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "password123");
  const btn = getByText("login.submit").parent!;
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeFalsy();
});

it("presents Google sign-in (constitution: parent auth is password + Google + Apple)", async () => {
  const { getByText } = await render(<LoginScreen />);
  expect(getByText("login.continueWithGoogle")).toBeTruthy();
});

it("the Google button stays disabled while the client id is still the unconfigured placeholder", async () => {
  const { getByText } = await render(<LoginScreen />);
  const btn = getByText("login.continueWithGoogle").parent!;
  expect(btn.props.accessibilityState?.disabled ?? btn.props.disabled).toBeTruthy();
});

it("calls login with trimmed organisation slug, email, and password", async () => {
  const replace = jest.fn();
  useRouter.mockReturnValue({ replace, push: jest.fn(), back: jest.fn() });

  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("acme-kdv"), "  org-a  ");
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "  user@test.com  ");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "password123");
  await fireEvent.press(getByText("login.submit"));

  await waitFor(() => expect(login).toHaveBeenCalledWith(expect.any(String), "org-a", "user@test.com", "password123"));
  await waitFor(() => expect(replace).toHaveBeenCalledWith("/(app)"));
});

it("shows error modal when login fails", async () => {
  login.mockRejectedValue(new Error("errors.auth.invalid_credentials"));

  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("acme-kdv"), "org-a");
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "user@test.com");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "wrongpass");
  await fireEvent.press(getByText("login.submit"));

  await waitFor(() => expect(getByText("errors.auth.invalid_credentials")).toBeTruthy());
});

it("shows the offline-first-login message when there is no network", async () => {
  login.mockRejectedValue(new Error("NETWORK_ERROR"));

  const { getByText, getByPlaceholderText } = await render(<LoginScreen />);
  await fireEvent.changeText(getByPlaceholderText("acme-kdv"), "org-a");
  await fireEvent.changeText(getByPlaceholderText("you@example.com"), "user@test.com");
  await fireEvent.changeText(getByPlaceholderText("••••••••"), "password123");
  await fireEvent.press(getByText("login.submit"));

  await waitFor(() => expect(getByText("login.offlineFirstLogin")).toBeTruthy());
});
