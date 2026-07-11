import React from "react";
import { render } from "@testing-library/react-native";
import GalleryScreen from "../app/(app)/gallery";
import type { GalleryResponse } from "../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../services/groupActivityGallery", () => ({
  getGroupActivityGallery: jest.fn(),
}));

const { getGroupActivityGallery } = jest.requireMock("../services/groupActivityGallery") as {
  getGroupActivityGallery: jest.Mock;
};

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
