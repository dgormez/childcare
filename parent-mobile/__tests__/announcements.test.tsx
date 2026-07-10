import React from "react";
import { render } from "@testing-library/react-native";
import AnnouncementScreen from "../app/(app)/announcements/[id]";
import type { ParentAnnouncementResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args) },
    __mockGet: mockGet,
  };
});

const getMock = (jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock }).__mockGet;
const { useLocalSearchParams } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

const announcement: ParentAnnouncementResponse = {
  id: "a1", subject: "Closed Friday", body: "We are closed early Friday for staff training.",
  sentAt: "2026-07-10T09:00:00Z", readAt: null,
};

beforeEach(() => {
  jest.clearAllMocks();
  useLocalSearchParams.mockReturnValue({ id: "a1" });
});

it("renders the announcement read-only, with no reply affordance (FR-009)", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, announcement));

  const { findByText, queryByPlaceholderText } = await render(<AnnouncementScreen />);

  expect(await findByText("Closed Friday")).toBeTruthy();
  expect(await findByText("We are closed early Friday for staff training.")).toBeTruthy();
  expect(await findByText("announcements.readOnly")).toBeTruthy();
  expect(queryByPlaceholderText(/message|reply/i)).toBeNull();
});
