import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import NotificationsScreen from "../app/(app)/notifications";
import type { NotificationResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts?.celsius !== undefined ? `${key}:${opts.celsius}` : key) }),
}));

jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  const mockPost = jest.fn().mockResolvedValue({ response: { ok: true } });
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args), POST: (...args: unknown[]) => mockPost(...args) },
    __mockGet: mockGet,
    __mockPost: mockPost,
  };
});

const apiMock = jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock; __mockPost: jest.Mock };
const getMock = apiMock.__mockGet;
const postMock = apiMock.__mockPost;
const { useRouter } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

const notifications: NotificationResponse[] = [
  {
    id: "n1", type: "newmessage", sourceId: "t1",
    titleKey: "parent.notifications.new_message.title", bodyKey: "parent.notifications.new_message.body",
    argumentsJson: "null", createdAt: "2026-07-10T12:00:00Z", readAt: null,
  },
  {
    id: "n2", type: "announcement", sourceId: "a1",
    titleKey: "parent.notifications.announcement.title", bodyKey: "parent.notifications.announcement.body",
    argumentsJson: "null", createdAt: "2026-07-10T11:00:00Z", readAt: null,
  },
  {
    id: "n3", type: "temperaturealert", sourceId: "e1",
    titleKey: "parent.notifications.temperature_alert.title", bodyKey: "parent.notifications.temperature_alert.body",
    argumentsJson: JSON.stringify({ celsius: 38.5 }), createdAt: "2026-07-10T10:00:00Z", readAt: null,
  },
];

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

it("renders all three notification types, each correctly typed", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));

  const { findByText } = await render(<NotificationsScreen />);

  expect(await findByText("parent.notifications.new_message.title")).toBeTruthy();
  expect(await findByText("parent.notifications.announcement.title")).toBeTruthy();
  expect(await findByText("parent.notifications.temperature_alert.title")).toBeTruthy();
  expect(await findByText("parent.notifications.temperature_alert.body:38.5")).toBeTruthy();
});

it("tapping a new-message notification marks it read and navigates to its thread", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText } = await render(<NotificationsScreen />);
  fireEvent.press(await findByText("parent.notifications.new_message.title"));

  await waitFor(() => expect(postMock).toHaveBeenCalledWith("/api/parent/notifications/{id}/read", { params: { path: { id: "n1" } } }));
  expect(push).toHaveBeenCalledWith("/(app)/messages/t1");
});

it("tapping an announcement notification navigates to its announcement", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText } = await render(<NotificationsScreen />);
  fireEvent.press(await findByText("parent.notifications.announcement.title"));

  expect(push).toHaveBeenCalledWith("/(app)/announcements/a1");
});

it("marking one notification read does not affect another's read state (FR-011)", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));

  const { findByText, getAllByTestId } = await render(<NotificationsScreen />);
  await findByText("parent.notifications.new_message.title");
  expect(getAllByTestId("unread-dot")).toHaveLength(3);

  fireEvent.press(await findByText("parent.notifications.new_message.title"));

  await waitFor(() => expect(getAllByTestId("unread-dot")).toHaveLength(2));
});
