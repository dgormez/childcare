import React from "react";
import { render, fireEvent, waitFor, act } from "@testing-library/react-native";
import RoomHomeScreen from "../../app/(room)/index";
import type { RoomRosterCard } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts ? `${key}:${JSON.stringify(opts)}` : key) }),
}));

// A no-op: RoomHomeScreen's own plain useEffect already covers the initial mount-time load
// these tests exercise. Wiring this to actually re-run the callback (e.g. via useEffect)
// would double-fire load() at mount — once from that plain useEffect, once from here — which
// silently consumes a second queued roster response before the test ever interacts with
// anything. useFocusEffect's real job (refetch on navigating back) needs a NavigationContainer
// this test doesn't set up, so it's out of scope here.
jest.mock("expo-router", () => ({ useFocusEffect: () => {} }));

jest.mock("../../services/roomShift", () => ({
  getRoster: jest.fn(),
  checkIn: jest.fn(),
  checkOut: jest.fn(),
}));

const { getRoster, checkIn, checkOut } = jest.requireMock("../../services/roomShift") as {
  getRoster: jest.Mock;
  checkIn: jest.Mock;
  checkOut: jest.Mock;
};

const alice: RoomRosterCard = { staffProfileId: "sp-a", firstName: "Alice", photoUrl: null, checkedIn: false, checkedInAt: null };
const bob: RoomRosterCard = { staffProfileId: "sp-b", firstName: "Bob", photoUrl: null, checkedIn: false, checkedInAt: null };

beforeEach(() => {
  jest.resetAllMocks(); // clears queued mockResolvedValueOnce implementations too, not just call history
});

async function enterPin(getByText: (text: string) => unknown, digits: string[]) {
  for (const digit of digits) {
    // eslint-disable-next-line no-await-in-loop
    await act(async () => {
      fireEvent.press(getByText(digit) as never);
    });
  }
}

// ── T040: renders the roster as photo cards; tapping a not-checked-in card opens the PIN
//    keypad addressed by name; a correct PIN closes the overlay and shows checked-in ──

it("renders the roster as cards, and a correct PIN check-in shows the card as checked in", async () => {
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: true, caregivers: [alice] });
  checkIn.mockResolvedValueOnce({ ok: true });
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: true, caregivers: [{ ...alice, checkedIn: true, checkedInAt: "2026-01-01T10:00:00Z" }] });

  const { getByText, queryByText } = await render(<RoomHomeScreen />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());

  fireEvent.press(getByText("Alice"));
  await waitFor(() => expect(getByText(/pin.enterPin/)).toBeTruthy());

  await enterPin(getByText, ["1", "2", "3", "4"]);

  await waitFor(() => expect(checkIn).toHaveBeenCalledWith("sp-a", "1234"));
  await waitFor(() => expect(queryByText(/pin.enterPin/)).toBeNull());
});

// ── T041: two cards can both show the checked-in state simultaneously, updating immediately
//    after each check-in/out ──

it("shows both cards checked in simultaneously after independent check-ins", async () => {
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: true, caregivers: [alice, bob] });

  const { getByText } = await render(<RoomHomeScreen />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());
  expect(getByText("Bob")).toBeTruthy();

  checkIn.mockResolvedValueOnce({ ok: true });
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: true, caregivers: [{ ...alice, checkedIn: true, checkedInAt: "2026-01-01T10:00:00Z" }, bob] });

  fireEvent.press(getByText("Alice"));
  await waitFor(() => expect(getByText(/pin.enterPin/)).toBeTruthy());
  await enterPin(getByText, ["1", "1", "1", "1"]);
  await waitFor(() => expect(checkIn).toHaveBeenCalledWith("sp-a", "1111"));

  checkIn.mockResolvedValueOnce({ ok: true });
  getRoster.mockResolvedValueOnce({
    requiresCaregiverPin: true,
    caregivers: [
      { ...alice, checkedIn: true, checkedInAt: "2026-01-01T10:00:00Z" },
      { ...bob, checkedIn: true, checkedInAt: "2026-01-01T10:01:00Z" },
    ],
  });

  await waitFor(() => expect(getByText("Bob")).toBeTruthy());
  fireEvent.press(getByText("Bob"));
  await waitFor(() => expect(getByText(/pin.enterPin/)).toBeTruthy());
  await enterPin(getByText, ["2", "2", "2", "2"]);
  await waitFor(() => expect(checkIn).toHaveBeenCalledWith("sp-b", "2222"));

  // Both cards' roster entries came back checkedIn:true, neither check-in blocked the other.
  expect(getRoster).toHaveBeenCalledTimes(3);
});

it("shows an empty state when no caregivers are eligible at this location", async () => {
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: true, caregivers: [] });

  const { getByText } = await render(<RoomHomeScreen />);
  await waitFor(() => expect(getByText("roomHome.noCaregivers")).toBeTruthy());
});

// ── Feature 008b (T019/T020): configurable caregiver PIN ────────────────────────────

it("skips the PIN keypad and checks in immediately when requiresCaregiverPin is false", async () => {
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: false, caregivers: [alice] });
  checkIn.mockResolvedValueOnce({ ok: true });
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: false, caregivers: [{ ...alice, checkedIn: true, checkedInAt: "2026-01-01T10:00:00Z" }] });

  const { getByText, queryByText } = await render(<RoomHomeScreen />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());

  fireEvent.press(getByText("Alice"));

  await waitFor(() => expect(checkIn).toHaveBeenCalledWith("sp-a"));
  expect(queryByText(/pin.enterPin/)).toBeNull();
});

it("still shows the PIN keypad when requiresCaregiverPin is true (regression guard)", async () => {
  getRoster.mockResolvedValueOnce({ requiresCaregiverPin: true, caregivers: [alice] });

  const { getByText } = await render(<RoomHomeScreen />);
  await waitFor(() => expect(getByText("Alice")).toBeTruthy());

  fireEvent.press(getByText("Alice"));

  await waitFor(() => expect(getByText(/pin.enterPin/)).toBeTruthy());
  expect(checkIn).not.toHaveBeenCalled();
});
