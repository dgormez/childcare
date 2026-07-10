import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import ThreadListScreen from "../app/(app)/messages/index";
import ThreadDetailScreen from "../app/(app)/messages/[id]";
import NewThreadScreen from "../app/(app)/messages/new";
import { useStore } from "../store/useStore";
import type { MessageThreadResponse, MessageThreadSummaryResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/apiClient", () => {
  const mockGet = jest.fn();
  const mockPost = jest.fn();
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args), POST: (...args: unknown[]) => mockPost(...args) },
    configureApiBaseUrl: jest.fn(),
    __mockGet: mockGet,
    __mockPost: mockPost,
  };
});

const apiMock = jest.requireMock("../services/apiClient") as { __mockGet: jest.Mock; __mockPost: jest.Mock };
const getMock = apiMock.__mockGet;
const postMock = apiMock.__mockPost;
const { useRouter, useLocalSearchParams } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status, json: async () => body }, data: ok ? body : undefined, error: ok ? undefined : body };
}

const threadSummary: MessageThreadSummaryResponse = {
  id: "t1", subject: "Nap question", childId: "c1", childName: "Timmy",
  lastActivityAt: "2026-07-10T10:00:00Z", hasUnread: true,
};

const threadDetail: MessageThreadResponse = {
  id: "t1", subject: "Nap question", childId: "c1", childName: "Timmy",
  createdAt: "2026-07-10T09:00:00Z", lastActivityAt: "2026-07-10T10:00:00Z", hasUnread: false,
  messages: [
    { id: "m1", threadId: "t1", senderId: "staff1", senderName: "Director Dana", body: "How can we help?", sentAt: "2026-07-10T09:00:00Z", readAt: null },
  ],
};

beforeEach(() => {
  jest.clearAllMocks();
  useStore.setState({ auth: { userId: "parent1", email: "p@test.com", role: "parent", organisationSlug: "org-a", accessToken: "tok" } });
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
  useLocalSearchParams.mockReturnValue({});
});

describe("thread list", () => {
  it("renders threads with an unread indicator", async () => {
    getMock.mockResolvedValueOnce(jsonResponse(200, [threadSummary]));

    const { findByText, getByTestId } = await render(<ThreadListScreen />);

    expect(await findByText("Timmy — Nap question")).toBeTruthy();
    expect(getByTestId("unread-dot")).toBeTruthy();
  });

  it("shows an empty state with no threads", async () => {
    getMock.mockResolvedValueOnce(jsonResponse(200, []));

    const { findByText } = await render(<ThreadListScreen />);
    expect(await findByText("messages.empty")).toBeTruthy();
  });

  it("navigates to the new-thread screen", async () => {
    getMock.mockResolvedValueOnce(jsonResponse(200, []));
    const push = jest.fn();
    useRouter.mockReturnValue({ push, replace: jest.fn(), back: jest.fn() });

    const { findByText } = await render(<ThreadListScreen />);
    await fireEvent.press(await findByText("messages.newThread"));

    expect(push).toHaveBeenCalledWith("/(app)/messages/new");
  });
});

describe("thread detail + compose", () => {
  it("renders message history in chronological order", async () => {
    useLocalSearchParams.mockReturnValue({ id: "t1" });
    getMock.mockResolvedValueOnce(jsonResponse(200, threadDetail));

    const { findByText } = await render(<ThreadDetailScreen />);
    expect(await findByText("How can we help?")).toBeTruthy();
    expect(await findByText("Director Dana")).toBeTruthy();
  });

  it("sends a reply via the send button and appends it to the thread", async () => {
    useLocalSearchParams.mockReturnValue({ id: "t1" });
    getMock.mockResolvedValueOnce(jsonResponse(200, threadDetail));
    postMock.mockResolvedValueOnce(jsonResponse(201, {
      id: "m2", threadId: "t1", senderId: "parent1", senderName: "Parent One",
      body: "Thanks!", sentAt: "2026-07-10T11:00:00Z", readAt: null,
    }));

    const { findByText, getByPlaceholderText, getByTestId } = await render(<ThreadDetailScreen />);
    await findByText("How can we help?");

    await fireEvent.changeText(getByPlaceholderText("messages.messagePlaceholder"), "Thanks!");
    await fireEvent.press(getByTestId("send-message-button"));

    await waitFor(() => expect(postMock).toHaveBeenCalledWith("/api/parent/message-threads/{id}/messages", {
      params: { path: { id: "t1" } },
      body: { body: "Thanks!" },
    }));
    expect(await findByText("Thanks!")).toBeTruthy();
  });
});

describe("start a new conversation", () => {
  it("creates a thread and navigates to it", async () => {
    getMock.mockResolvedValueOnce(jsonResponse(200, [{ id: "c1", firstName: "Timmy", lastName: "Tester", photoDownloadUrl: null, dateOfBirth: "2022-01-01" }]));
    postMock.mockResolvedValueOnce(jsonResponse(201, threadDetail));
    const replace = jest.fn();
    useRouter.mockReturnValue({ replace, push: jest.fn(), back: jest.fn() });

    const { findByText, getByText, getByPlaceholderText } = await render(<NewThreadScreen />);
    await findByText("Timmy Tester");

    await fireEvent.changeText(getByPlaceholderText("messages.subjectPlaceholder"), "Nap question");
    await fireEvent.changeText(getByPlaceholderText("messages.firstMessagePlaceholder"), "Can she nap longer?");
    await fireEvent.press(getByText("messages.send"));

    await waitFor(() => expect(postMock).toHaveBeenCalledWith("/api/parent/message-threads", {
      body: { childId: null, subject: "Nap question", body: "Can she nap longer?" },
    }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/(app)/messages/t1"));
  });
});
