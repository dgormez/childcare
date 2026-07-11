import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { AddGroupActivitySheet } from "../../components/AddGroupActivitySheet";
import type { GroupActivityResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/groupActivities", () => ({
  createGroupActivity: jest.fn(),
}));

jest.mock("../../services/photoUploadQueue", () => ({
  enqueuePhotoUpload: jest.fn().mockResolvedValue(undefined),
  uploadPendingPhotos: jest.fn().mockResolvedValue(undefined),
}));

const mockUseNetworkStatus = jest.fn();
jest.mock("../../hooks/useNetworkStatus", () => ({ useNetworkStatus: () => mockUseNetworkStatus() }));

jest.mock("expo-image-picker", () => ({
  requestCameraPermissionsAsync: jest.fn(),
  requestMediaLibraryPermissionsAsync: jest.fn(),
  launchCameraAsync: jest.fn(),
  launchImageLibraryAsync: jest.fn(),
}));

const { createGroupActivity } = jest.requireMock("../../services/groupActivities") as { createGroupActivity: jest.Mock };
const { enqueuePhotoUpload, uploadPendingPhotos } = jest.requireMock("../../services/photoUploadQueue") as {
  enqueuePhotoUpload: jest.Mock;
  uploadPendingPhotos: jest.Mock;
};
const ImagePicker = jest.requireMock("expo-image-picker") as {
  requestMediaLibraryPermissionsAsync: jest.Mock;
  launchImageLibraryAsync: jest.Mock;
};

const fakeActivity = { id: "a1", title: "In de tuin", photos: [] } as unknown as GroupActivityResponse;

beforeEach(() => {
  jest.clearAllMocks();
  mockUseNetworkStatus.mockReturnValue({ isConnected: true });
  createGroupActivity.mockResolvedValue(fakeActivity);
});

it("creates an activity with a pre-filled, editable title after picking a type", async () => {
  const onActivityRecorded = jest.fn();
  const { getByText, getByDisplayValue } = await render(
    <AddGroupActivitySheet visible onClose={jest.fn()} onActivityRecorded={onActivityRecorded} />
  );

  fireEvent.press(getByText("groupActivities.types.outdoor"));
  await waitFor(() => expect(getByDisplayValue("groupActivities.types.outdoor")).toBeTruthy());

  await act(async () => fireEvent.press(getByText("groupActivities.save")));

  await waitFor(() =>
    expect(createGroupActivity).toHaveBeenCalledWith(
      expect.objectContaining({ activityType: "outdoor", title: "groupActivities.types.outdoor" }),
      true
    )
  );
  expect(enqueuePhotoUpload).not.toHaveBeenCalled();
  await waitFor(() => expect(onActivityRecorded).toHaveBeenCalledWith(fakeActivity));
});

it("blocks saving when the title is cleared out", async () => {
  const { getByText, getByDisplayValue } = await render(
    <AddGroupActivitySheet visible onClose={jest.fn()} onActivityRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("groupActivities.types.story"));
  const titleInput = await waitFor(() => getByDisplayValue("groupActivities.types.story"));
  await act(async () => fireEvent.changeText(titleInput, "   "));

  await act(async () => fireEvent.press(getByText("groupActivities.save")));

  expect(createGroupActivity).not.toHaveBeenCalled();
});

it("blocks adding an 11th photo", async () => {
  ImagePicker.requestMediaLibraryPermissionsAsync.mockResolvedValue({ granted: true });
  ImagePicker.launchImageLibraryAsync.mockResolvedValue({
    canceled: false,
    assets: Array.from({ length: 10 }, (_, i) => ({ uri: `file://photo-${i}.jpg` })),
  });

  const { getByText } = await render(
    <AddGroupActivitySheet visible onClose={jest.fn()} onActivityRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("groupActivities.types.other"));
  await waitFor(() => expect(getByText("groupActivities.photoGallery")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("groupActivities.photoGallery")));

  expect(ImagePicker.launchImageLibraryAsync).toHaveBeenCalledWith(expect.objectContaining({ selectionLimit: 10 }));

  await act(async () => fireEvent.press(getByText("groupActivities.photoGallery")));
  expect(ImagePicker.launchImageLibraryAsync).toHaveBeenCalledTimes(1); // 10 already attached — second tap is blocked, not a second picker call
});

it("enqueues one photo-upload row per attached photo on save, then triggers the uploader", async () => {
  ImagePicker.requestMediaLibraryPermissionsAsync.mockResolvedValue({ granted: true });
  ImagePicker.launchImageLibraryAsync.mockResolvedValue({
    canceled: false,
    assets: [{ uri: "file://photo-1.jpg" }, { uri: "file://photo-2.jpg" }],
  });

  const { getByText } = await render(
    <AddGroupActivitySheet visible onClose={jest.fn()} onActivityRecorded={jest.fn()} />
  );

  fireEvent.press(getByText("groupActivities.types.music"));
  await waitFor(() => expect(getByText("groupActivities.photoGallery")).toBeTruthy());
  await act(async () => fireEvent.press(getByText("groupActivities.photoGallery")));
  await act(async () => fireEvent.press(getByText("groupActivities.save")));

  await waitFor(() => expect(createGroupActivity).toHaveBeenCalled());
  expect(enqueuePhotoUpload).toHaveBeenCalledWith("a1", "file://photo-1.jpg");
  expect(enqueuePhotoUpload).toHaveBeenCalledWith("a1", "file://photo-2.jpg");
  expect(uploadPendingPhotos).toHaveBeenCalled();
});
