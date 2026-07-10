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

const postMock = (jest.requireMock("../../services/apiClient") as { __mockPost: jest.Mock }).__mockPost;
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
