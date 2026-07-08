import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import { AdministratorConfirmation } from "../../components/AdministratorConfirmation";
import type { RoomRosterCard } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts ? `${key}:${JSON.stringify(opts)}` : key) }),
}));

jest.mock("../../services/roomShift", () => ({
  getRoster: jest.fn(),
  confirmAdministrator: jest.fn(),
}));

const mockUseNetworkStatus = jest.fn();
jest.mock("../../hooks/useNetworkStatus", () => ({ useNetworkStatus: () => mockUseNetworkStatus() }));

const { getRoster, confirmAdministrator } = jest.requireMock("../../services/roomShift") as {
  getRoster: jest.Mock;
  confirmAdministrator: jest.Mock;
};

const alice: RoomRosterCard = { staffProfileId: "sp-a", firstName: "Alice", photoUrl: null, checkedIn: true, checkedInAt: "2026-01-01T09:00:00Z" };
const bob: RoomRosterCard = { staffProfileId: "sp-b", firstName: "Bob", photoUrl: null, checkedIn: false, checkedInAt: null };

beforeEach(() => {
  jest.resetAllMocks();
  mockUseNetworkStatus.mockReturnValue({ isConnected: true });
});

async function enterPin(getByText: (text: string) => unknown, digits: string[]) {
  for (const digit of digits) {
    // eslint-disable-next-line no-await-in-loop
    await act(async () => {
      fireEvent.press(getByText(digit) as never);
    });
  }
}

it("shows only the currently-checked-in roster as cards", async () => {
  getRoster.mockResolvedValueOnce([alice, bob]);

  const { getByText, queryByText } = await render(<AdministratorConfirmation onComplete={jest.fn()} />);

  await waitFor(() => expect(getByText("Alice")).toBeTruthy());
  expect(queryByText("Bob")).toBeNull();
});

it("calls confirmAdministrator with the tapped card's staffId when online, and completes with the confirmed id", async () => {
  getRoster.mockResolvedValueOnce([alice]);
  confirmAdministrator.mockResolvedValueOnce({ ok: true, administeredByStaffProfileId: "sp-a" });
  const onComplete = jest.fn();

  const { getByText } = await render(<AdministratorConfirmation onComplete={onComplete} />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());

  fireEvent.press(getByText("Alice"));
  await waitFor(() => expect(getByText(/pin.enterPin/)).toBeTruthy());
  await enterPin(getByText, ["1", "2", "3", "4"]);

  await waitFor(() => expect(confirmAdministrator).toHaveBeenCalledWith("sp-a", "1234", false));
  await waitFor(() => expect(onComplete).toHaveBeenCalledWith("sp-a"));
});

it("skip online calls confirmAdministrator with skip:true and completes with its response", async () => {
  getRoster.mockResolvedValueOnce([alice]);
  confirmAdministrator.mockResolvedValueOnce({ ok: true, administeredByStaffProfileId: null });
  const onComplete = jest.fn();

  const { getByText } = await render(<AdministratorConfirmation onComplete={onComplete} />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());

  fireEvent.press(getByText("pin.skip"));

  await waitFor(() => expect(confirmAdministrator).toHaveBeenCalledWith(null, null, true));
  await waitFor(() => expect(onComplete).toHaveBeenCalledWith(null));
});

// ── US5 AC3: when offline, skip resolves to null locally without an API call at all ──

it("skip while offline resolves to null locally, without calling confirmAdministrator", async () => {
  mockUseNetworkStatus.mockReturnValue({ isConnected: false });
  getRoster.mockResolvedValueOnce([alice]);
  const onComplete = jest.fn();

  const { getByText } = await render(<AdministratorConfirmation onComplete={onComplete} />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());

  fireEvent.press(getByText("pin.skip"));

  await waitFor(() => expect(onComplete).toHaveBeenCalledWith(null));
  expect(confirmAdministrator).not.toHaveBeenCalled();
});
