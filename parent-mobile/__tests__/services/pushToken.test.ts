jest.mock("expo-notifications", () => ({
  getPermissionsAsync: jest.fn(),
  requestPermissionsAsync: jest.fn(),
  getExpoPushTokenAsync: jest.fn(),
}));

jest.mock("../../services/apiClient", () => ({
  apiClient: { PUT: jest.fn().mockResolvedValue({ response: { ok: true } }) },
}));

jest.mock("expo-constants", () => ({
  expoConfig: { extra: { eas: { projectId: "test-project-id" } } },
}));

import * as Notifications from "expo-notifications";
import { apiClient } from "../../services/apiClient";
import { registerPushToken } from "../../services/pushToken";

const putMock = (apiClient as unknown as { PUT: jest.Mock }).PUT;

beforeEach(() => {
  jest.clearAllMocks();
});

it("registers the Expo push token when permission is already granted (FR-014)", async () => {
  (Notifications.getPermissionsAsync as jest.Mock).mockResolvedValue({ status: "granted" });
  (Notifications.getExpoPushTokenAsync as jest.Mock).mockResolvedValue({ data: "ExponentPushToken[abc]" });

  await registerPushToken();

  expect(Notifications.requestPermissionsAsync).not.toHaveBeenCalled();
  expect(putMock).toHaveBeenCalledWith("/api/parent/push-token", { body: { pushToken: "ExponentPushToken[abc]" } });
});

it("requests permission when not already granted, and registers on approval", async () => {
  (Notifications.getPermissionsAsync as jest.Mock).mockResolvedValue({ status: "undetermined" });
  (Notifications.requestPermissionsAsync as jest.Mock).mockResolvedValue({ status: "granted" });
  (Notifications.getExpoPushTokenAsync as jest.Mock).mockResolvedValue({ data: "ExponentPushToken[abc]" });

  await registerPushToken();

  expect(Notifications.requestPermissionsAsync).toHaveBeenCalled();
  expect(putMock).toHaveBeenCalled();
});

it("does not register a token when permission is denied", async () => {
  (Notifications.getPermissionsAsync as jest.Mock).mockResolvedValue({ status: "denied" });
  (Notifications.requestPermissionsAsync as jest.Mock).mockResolvedValue({ status: "denied" });

  await registerPushToken();

  expect(putMock).not.toHaveBeenCalled();
});

it("does not attempt to fetch a push token when the EAS project id is still the unconfigured placeholder", async () => {
  jest.resetModules();
  jest.doMock("expo-constants", () => ({ expoConfig: { extra: { eas: { projectId: "YOUR_EAS_PROJECT_ID" } } } }));
  jest.doMock("expo-notifications", () => ({
    getPermissionsAsync: jest.fn().mockResolvedValue({ status: "granted" }),
    requestPermissionsAsync: jest.fn(),
    getExpoPushTokenAsync: jest.fn(),
  }));
  jest.doMock("../../services/apiClient", () => ({
    apiClient: { PUT: jest.fn().mockResolvedValue({ response: { ok: true } }) },
  }));

  const { registerPushToken: registerWithPlaceholder } = require("../../services/pushToken");
  const notifications = require("expo-notifications");
  const { apiClient: reloadedApiClient } = require("../../services/apiClient");

  await registerWithPlaceholder();

  expect(notifications.getExpoPushTokenAsync).not.toHaveBeenCalled();
  expect(reloadedApiClient.PUT).not.toHaveBeenCalled();
});
