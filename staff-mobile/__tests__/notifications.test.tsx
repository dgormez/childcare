import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import NotificationsScreen from "../app/(app)/notifications";
import type { NotificationResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
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
  return { response: { ok, status }, data: ok ? body : undefined, error: ok ? undefined : body };
}

// research.md R6: only the three staff-facing notification types.
const notifications: NotificationResponse[] = [
  { id: "n1", type: "schedulepublished", sourceId: "s1", titleKey: "staff.notifications.schedule_published.title", bodyKey: "staff.notifications.schedule_published.body", argumentsJson: "{}", createdAt: "2026-07-10T12:00:00Z", readAt: null },
  { id: "n2", type: "assignmentchanged", sourceId: "s2", titleKey: "staff.notifications.assignment_changed.title", bodyKey: "staff.notifications.assignment_changed.body", argumentsJson: "{}", createdAt: "2026-07-10T11:00:00Z", readAt: null },
  { id: "n3", type: "leaverequestdecided", sourceId: "l1", titleKey: "staff.notifications.leave_request_decided.title", bodyKey: "staff.notifications.leave_request_decided.approved_body", argumentsJson: "{}", createdAt: "2026-07-10T10:00:00Z", readAt: null },
];

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

it("renders all three staff notification types without throwing (research.md R6)", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));
  const { findByText } = await render(<NotificationsScreen />);

  expect(await findByText("staff.notifications.schedule_published.title")).toBeTruthy();
  expect(await findByText("staff.notifications.assignment_changed.title")).toBeTruthy();
  expect(await findByText("staff.notifications.leave_request_decided.title")).toBeTruthy();
});

it("tapping a schedule-published notification marks it read and navigates to the schedule", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText } = await render(<NotificationsScreen />);
  fireEvent.press(await findByText("staff.notifications.schedule_published.title"));

  await waitFor(() => expect(postMock).toHaveBeenCalledWith("/api/staff/notifications/{id}/read", { params: { path: { id: "n1" } } }));
  expect(push).toHaveBeenCalledWith("/(app)/schedule");
});

it("tapping a leave-request-decided notification navigates to leave requests", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));
  const push = jest.fn();
  useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

  const { findByText } = await render(<NotificationsScreen />);
  fireEvent.press(await findByText("staff.notifications.leave_request_decided.title"));

  expect(push).toHaveBeenCalledWith("/(app)/leave-requests");
});

it("marking one notification read does not affect another's read state", async () => {
  getMock.mockResolvedValueOnce(jsonResponse(200, notifications));
  const { findByText, getAllByTestId } = await render(<NotificationsScreen />);
  await findByText("staff.notifications.schedule_published.title");
  expect(getAllByTestId("unread-dot")).toHaveLength(3);

  fireEvent.press(await findByText("staff.notifications.schedule_published.title"));

  await waitFor(() => expect(getAllByTestId("unread-dot")).toHaveLength(2));
});
