import React from "react";
import { render, act, fireEvent } from "@testing-library/react-native";
import AppLayout from "../../app/(app)/_layout";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock("../../services/syncEngine", () => ({
  syncPendingQueue: jest.fn().mockResolvedValue({ succeeded: 0, failed: 0, conflicted: 0 }),
}));

jest.mock("../../hooks/useSyncStatus", () => {
  const mockSyncStatus = { pendingCount: 0, lastSyncedAt: null, isSyncing: false };
  return {
    useSyncStatus: () => mockSyncStatus,
    __mockSyncStatus: mockSyncStatus,
  };
});

jest.mock("../../services/auth", () => ({ logout: jest.fn() }));

const { useRouter } = require("expo-router");
const netInfoMock = require("@react-native-community/netinfo");
const syncStatusMock = jest.requireMock("../../hooks/useSyncStatus") as { __mockSyncStatus: { pendingCount: number; isSyncing: boolean } };
const authMock = jest.requireMock("../../services/auth") as { logout: jest.Mock };

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
  netInfoMock.__setConnected(true);
  syncStatusMock.__mockSyncStatus.pendingCount = 0;
  syncStatusMock.__mockSyncStatus.isSyncing = false;
});

// handleLogout/doLogout are async (they await logout()); firing the press event and awaiting
// its full effect must happen inside the SAME act() scope, or the promise chain resolves after
// the act() call returns and can bleed state updates into whichever test renders next.
async function press(getByText: (text: string) => unknown, text: string) {
  await act(async () => {
    fireEvent.press(getByText(text) as never);
  });
}

it("logs out immediately, with no confirmation, when nothing is pending", async () => {
  const { getByText, queryByText } = await render(<AppLayout />);

  await press(getByText, "logout.confirm");

  expect(queryByText("logout.confirmPendingMessage")).toBeNull();
  expect(authMock.logout).toHaveBeenCalledTimes(1);
});

it("shows a confirmation dialog before logout when actions are still pending (Edge Cases)", async () => {
  syncStatusMock.__mockSyncStatus.pendingCount = 3;
  const { getByText, queryByText } = await render(<AppLayout />);

  await press(getByText, "logout.confirm");

  expect(queryByText("logout.confirmPendingMessage")).toBeTruthy();
  expect(authMock.logout).not.toHaveBeenCalled();
});

it("cancelling the pending-actions confirmation does not log out", async () => {
  syncStatusMock.__mockSyncStatus.pendingCount = 3;
  const { getByText, queryByText } = await render(<AppLayout />);

  await press(getByText, "logout.confirm");
  await press(getByText, "logout.cancel");

  expect(queryByText("logout.confirmPendingMessage")).toBeNull();
  expect(authMock.logout).not.toHaveBeenCalled();
});

it("confirming the pending-actions dialog proceeds with logout", async () => {
  syncStatusMock.__mockSyncStatus.pendingCount = 3;
  const { getByText, getAllByText } = await render(<AppLayout />);

  // Only the header button exists before the modal opens.
  await act(async () => {
    fireEvent.press(getByText("logout.confirm"));
  });
  // Once open, ThemedModal renders before the header in the tree, so its destructive
  // "logout.confirm" button is the first match, not the last.
  await act(async () => {
    fireEvent.press(getAllByText("logout.confirm")[0]);
  });

  expect(authMock.logout).toHaveBeenCalledTimes(1);
});
