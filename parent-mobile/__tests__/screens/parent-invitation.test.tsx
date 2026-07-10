import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import ParentInvitationScreen from "../../app/(auth)/parent-invitation";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/apiClient", () => {
  const mockPost = jest.fn();
  return {
    apiClient: { POST: (...args: unknown[]) => mockPost(...args) },
    __mockPost: mockPost,
  };
});

jest.mock("../../services/auth", () => ({
  loginWithGoogle: jest.fn(),
  loginWithApple: jest.fn(),
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

const postMock = (jest.requireMock("../../services/apiClient") as { __mockPost: jest.Mock }).__mockPost;
const { loginWithGoogle } = require("../../services/auth");
const { useLocalSearchParams, useRouter } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

beforeEach(() => {
  jest.clearAllMocks();
  useLocalSearchParams.mockReturnValue({ token: "tok-123", org: "org-a" });
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

it("shows an invalid-link message when the deep link is missing token/org", async () => {
  useLocalSearchParams.mockReturnValue({});
  const { findByText } = await render(<ParentInvitationScreen />);
  expect(await findByText("parentInvitation.invalidLink")).toBeTruthy();
});

it("rejects mismatched passwords before calling the API", async () => {
  const { getByText, getAllByPlaceholderText, findByText } = await render(<ParentInvitationScreen />);
  const fields = getAllByPlaceholderText("••••••••");
  await fireEvent.changeText(fields[0], "password123");
  await fireEvent.changeText(fields[1], "different456");
  await fireEvent.press(getByText("parentInvitation.submit"));

  expect(await findByText("parentInvitation.passwordMismatch")).toBeTruthy();
  expect(postMock).not.toHaveBeenCalled();
});

it("accepts the invitation and shows a success state leading to sign-in", async () => {
  postMock.mockResolvedValueOnce(jsonResponse(200, {}));

  const { getByText, getAllByPlaceholderText, findByText } = await render(<ParentInvitationScreen />);
  const fields = getAllByPlaceholderText("••••••••");
  await fireEvent.changeText(fields[0], "password123");
  await fireEvent.changeText(fields[1], "password123");
  await fireEvent.press(getByText("parentInvitation.submit"));

  await waitFor(() => expect(postMock).toHaveBeenCalledWith("/api/parent-invitations/accept", {
    body: { organisationSlug: "org-a", token: "tok-123", password: "password123" },
  }));
  expect(await findByText("parentInvitation.success")).toBeTruthy();
});

it("shows a generic, non-enumerable error for an expired or already-used token", async () => {
  postMock.mockResolvedValueOnce(jsonResponse(404, { errorKey: "errors.invitation.not_found" }));

  const { getByText, getAllByPlaceholderText, findByText } = await render(<ParentInvitationScreen />);
  const fields = getAllByPlaceholderText("••••••••");
  await fireEvent.changeText(fields[0], "password123");
  await fireEvent.changeText(fields[1], "password123");
  await fireEvent.press(getByText("parentInvitation.submit"));

  expect(await findByText("errors.invitation.not_found")).toBeTruthy();
});

// Convergence F3/FR-000b: completing an invitation via Google/Apple, not just a password,
// must be reachable straight from the invitation deep link itself.

it("offers Google sign-in as an alternative way to complete the invitation", async () => {
  const { findByText } = await render(<ParentInvitationScreen />);
  expect(await findByText("login.continueWithGoogle")).toBeTruthy();
});

it("completes the invitation via Google sign-in using the org slug from the deep link", async () => {
  loginWithGoogle.mockResolvedValue(undefined);
  const replace = jest.fn();
  useRouter.mockReturnValue({ replace, push: jest.fn(), back: jest.fn() });
  const { useAuthRequest } = require("expo-auth-session/providers/google");
  useAuthRequest.mockReturnValue([null, {
    type: "success",
    authentication: { idToken: "google-id-token" },
  }, jest.fn()]);

  await render(<ParentInvitationScreen />);

  await waitFor(() => expect(loginWithGoogle).toHaveBeenCalledWith(expect.any(String), "org-a", "google-id-token"));
  await waitFor(() => expect(replace).toHaveBeenCalledWith("/(app)"));
});
