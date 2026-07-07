import React from "react";
import { render, act } from "@testing-library/react-native";
import { AppState } from "react-native";
import AppLayout from "../../app/(app)/_layout";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/syncEngine", () => {
  const mockSyncPendingQueue = jest.fn().mockResolvedValue({ succeeded: 0, failed: 0, conflicted: 0 });
  return {
    syncPendingQueue: (...args: unknown[]) => mockSyncPendingQueue(...args),
    __mockSyncPendingQueue: mockSyncPendingQueue,
  };
});

jest.mock("../../hooks/useSyncStatus", () => ({
  useSyncStatus: () => ({ pendingCount: 0, lastSyncedAt: null, isSyncing: false }),
}));

jest.mock("../../services/auth", () => ({ logout: jest.fn() }));

const { useRouter } = require("expo-router");
const netInfoMock = require("@react-native-community/netinfo");
const syncEngineMock = jest.requireMock("../../services/syncEngine") as { __mockSyncPendingQueue: jest.Mock };

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
  netInfoMock.__setConnected(true);
});

it("triggers a sync when the network transitions from offline to online, not while already online (FR-012a)", async () => {
  netInfoMock.__setConnected(false);
  await render(<AppLayout />);
  expect(syncEngineMock.__mockSyncPendingQueue).not.toHaveBeenCalled();

  await act(async () => {
    netInfoMock.__setConnected(true);
  });
  expect(syncEngineMock.__mockSyncPendingQueue).toHaveBeenCalledTimes(1);
});

it("does not trigger a sync merely from re-rendering while already online", async () => {
  await render(<AppLayout />);
  expect(syncEngineMock.__mockSyncPendingQueue).not.toHaveBeenCalled();
});

it("triggers a sync when the app returns to the foreground (FR-012a)", async () => {
  await render(<AppLayout />);
  syncEngineMock.__mockSyncPendingQueue.mockClear();

  const addEventListenerMock = AppState.addEventListener as jest.Mock;
  const [, onChange] = addEventListenerMock.mock.calls[addEventListenerMock.mock.calls.length - 1];

  await act(async () => {
    onChange("active");
  });
  expect(syncEngineMock.__mockSyncPendingQueue).toHaveBeenCalledTimes(1);
});

it("does not trigger a sync when the app goes to the background", async () => {
  await render(<AppLayout />);
  syncEngineMock.__mockSyncPendingQueue.mockClear();

  const addEventListenerMock = AppState.addEventListener as jest.Mock;
  const [, onChange] = addEventListenerMock.mock.calls[addEventListenerMock.mock.calls.length - 1];

  await act(async () => {
    onChange("background");
  });
  expect(syncEngineMock.__mockSyncPendingQueue).not.toHaveBeenCalled();
});
