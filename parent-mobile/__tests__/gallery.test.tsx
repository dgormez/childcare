import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import * as Network from "expo-network";
import GalleryScreen from "../app/(app)/gallery";
import type { GalleryResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/groupActivityGallery", () => ({
  getGroupActivityGallery: jest.fn(),
  downloadGroupActivityPhotoOriginal: jest.fn(),
}));

jest.mock("expo-sharing", () => ({
  isAvailableAsync: jest.fn().mockResolvedValue(true),
  shareAsync: jest.fn().mockResolvedValue(undefined),
}));

jest.mock("react-native-toast-message", () => ({ show: jest.fn() }));

const { getGroupActivityGallery, downloadGroupActivityPhotoOriginal } = jest.requireMock("../services/groupActivityGallery") as {
  getGroupActivityGallery: jest.Mock;
  downloadGroupActivityPhotoOriginal: jest.Mock;
};
const Sharing = jest.requireMock("expo-sharing") as { isAvailableAsync: jest.Mock; shareAsync: jest.Mock };
const Toast = jest.requireMock("react-native-toast-message") as { show: jest.Mock };

const photo = (id: string) => ({
  activityId: `activity-${id}`,
  groupId: "group-1",
  occurredAt: "2026-07-11T09:00:00.000Z",
  photo: { id, downloadUrl: `https://example.test/${id}.jpg`, thumbnailDownloadUrl: `https://example.test/${id}-thumb.jpg`, caption: null, uploadedAt: "2026-07-11T09:05:00.000Z" },
});

beforeEach(() => {
  jest.clearAllMocks();
});

it("renders a photo grid when the parent has consent and photos exist this month", async () => {
  const response: GalleryResponse = { items: [photo("p1"), photo("p2")], hasConsent: true };
  getGroupActivityGallery.mockResolvedValue(response);

  const { findByTestId } = await render(<GalleryScreen />);

  expect(await findByTestId("gallery-photo-p1")).toBeTruthy();
  expect(await findByTestId("gallery-photo-p2")).toBeTruthy();
});

it("shows the no-consent empty state (not a blank grid) when hasConsent is false", async () => {
  getGroupActivityGallery.mockResolvedValue({ items: [], hasConsent: false });

  const { findByText } = await render(<GalleryScreen />);

  expect(await findByText("gallery.noConsent")).toBeTruthy();
});

it("shows a plain empty state when the parent has consent but nothing was recorded this month", async () => {
  getGroupActivityGallery.mockResolvedValue({ items: [], hasConsent: true });

  const { findByText } = await render(<GalleryScreen />);

  expect(await findByText("gallery.empty")).toBeTruthy();
});

it("aggregates photos across multiple groups/children into one grid", async () => {
  const response: GalleryResponse = {
    items: [
      { ...photo("p1"), groupId: "group-a" },
      { ...photo("p2"), groupId: "group-b" },
    ],
    hasConsent: true,
  };
  getGroupActivityGallery.mockResolvedValue(response);

  const { findByTestId } = await render(<GalleryScreen />);

  expect(await findByTestId("gallery-photo-p1")).toBeTruthy();
  expect(await findByTestId("gallery-photo-p2")).toBeTruthy();
});

it("opens the full-resolution detail view and downloads the original on tap", async () => {
  getGroupActivityGallery.mockResolvedValue({ items: [photo("p1")], hasConsent: true });
  downloadGroupActivityPhotoOriginal.mockResolvedValue({ uri: "file:///cache/group-activity-photos/p1.jpg" });

  const { findByTestId, findByText } = await render(<GalleryScreen />);
  fireEvent.press(await findByTestId("gallery-photo-p1"));

  fireEvent.press(await findByText("gallery.downloadOriginal"));

  await waitFor(() => expect(downloadGroupActivityPhotoOriginal).toHaveBeenCalledWith("p1"));
  await waitFor(() => expect(Sharing.shareAsync).toHaveBeenCalledWith("file:///cache/group-activity-photos/p1.jpg", expect.any(Object)));
});

it("shows a toast when the download fails, without crashing the detail view", async () => {
  getGroupActivityGallery.mockResolvedValue({ items: [photo("p1")], hasConsent: true });
  downloadGroupActivityPhotoOriginal.mockRejectedValue(new Error("network"));

  const { findByTestId, findByText } = await render(<GalleryScreen />);
  fireEvent.press(await findByTestId("gallery-photo-p1"));
  fireEvent.press(await findByText("gallery.downloadOriginal"));

  await waitFor(() => expect(Toast.show).toHaveBeenCalledWith(expect.objectContaining({ type: "error", text1: "gallery.downloadFailed" })));
});

it("hides the download action while offline", async () => {
  (Network.getNetworkStateAsync as jest.Mock).mockResolvedValueOnce({ isConnected: false, isInternetReachable: false });
  getGroupActivityGallery.mockResolvedValue({ items: [photo("p1")], hasConsent: true });

  const { findByTestId, queryByText } = await render(<GalleryScreen />);
  fireEvent.press(await findByTestId("gallery-photo-p1"));

  await waitFor(() => expect(queryByText("gallery.downloadOriginal")).toBeNull());
});
