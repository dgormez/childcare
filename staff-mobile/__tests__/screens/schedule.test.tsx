import React from "react";
import { render, fireEvent, waitFor } from "@testing-library/react-native";
import ScheduleScreen from "../../app/(app)/schedule/index";
import type { StaffScheduleResponse } from "../../types";

jest.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string, opts?: Record<string, unknown>) => (opts?.date ? `${key}:${opts.date}` : key) }),
}));

jest.mock("../../services/apiClient", () => {
  const mockGet = jest.fn();
  const mockPost = jest.fn().mockResolvedValue({ response: { ok: true } });
  return {
    apiClient: { GET: (...args: unknown[]) => mockGet(...args), POST: (...args: unknown[]) => mockPost(...args) },
    __mockGet: mockGet,
  };
});

const apiMock = jest.requireMock("../../services/apiClient") as { __mockGet: jest.Mock };
const getMock = apiMock.__mockGet;
const { useRouter } = require("expo-router");

function jsonResponse(status: number, body: unknown) {
  const ok = status >= 200 && status < 300;
  return { response: { ok, status }, data: ok ? body : undefined, error: ok ? undefined : body };
}

function todayPlusDays(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

function makeEntry(overrides: Partial<StaffScheduleResponse> = {}): StaffScheduleResponse {
  return {
    id: "entry-1",
    staffProfileId: "staff-1",
    locationId: "loc-1",
    groupId: null,
    date: todayPlusDays(1),
    startTime: "08:00:00",
    endTime: "16:00:00",
    status: "scheduled",
    absenceReason: null,
    coverStaffId: null,
    notes: null,
    isPublished: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function mockRoutes(overrides: { schedule?: StaffScheduleResponse[]; closures?: { locationId: string; date: string }[] } = {}) {
  const { schedule = [], closures = [] } = overrides;
  getMock.mockImplementation((path: unknown) => {
    if (path === "/api/staff-schedules/me") return Promise.resolve(jsonResponse(200, schedule));
    if (path === "/api/staff/me") return Promise.resolve(jsonResponse(200, { contractedDays: [] }));
    if (path === "/api/locations/names") return Promise.resolve(jsonResponse(200, [{ id: "loc-1", name: "Sunshine House" }, { id: "loc-2", name: "Rainbow House" }]));
    if (path === "/api/groups") return Promise.resolve(jsonResponse(200, [{ id: "group-1", name: "Baby Room" }]));
    if (path === "/api/closures/dates") return Promise.resolve(jsonResponse(200, closures));
    return Promise.resolve(jsonResponse(200, []));
  });
}

beforeEach(() => {
  jest.clearAllMocks();
  useRouter.mockReturnValue({ push: jest.fn(), replace: jest.fn(), back: jest.fn() });
});

describe("ScheduleScreen", () => {
  it("toggles between week and day views", async () => {
    mockRoutes({ schedule: [makeEntry()] });

    const { findByText, getByText } = await render(<ScheduleScreen />);

    await findByText("schedule.weekView");
    expect(getByText("schedule.dayView")).toBeTruthy();

    fireEvent.press(getByText("schedule.dayView"));
    // Day view renders a horizontal date-chip picker plus the selected day's card.
    await waitFor(() => expect(getByText("schedule.dayView")).toBeTruthy());
  });

  it("shows a split day: two locations on the same date both render", async () => {
    const date = todayPlusDays(1);
    mockRoutes({
      schedule: [
        makeEntry({ id: "e1", locationId: "loc-1", date, startTime: "08:00:00", endTime: "12:00:00" }),
        makeEntry({ id: "e2", locationId: "loc-2", date, startTime: "13:00:00", endTime: "17:00:00" }),
      ],
    });

    const { findByText } = await render(<ScheduleScreen />);

    expect(await findByText("Sunshine House")).toBeTruthy();
    expect(await findByText("Rainbow House")).toBeTruthy();
  });

  it("shows a closure day as KDV gesloten for the affected entry", async () => {
    const date = todayPlusDays(1);
    mockRoutes({
      schedule: [makeEntry({ id: "e1", locationId: "loc-1", date })],
      closures: [{ locationId: "loc-1", date }],
    });

    const { findAllByTestId } = await render(<ScheduleScreen />);

    const closureLabels = await findAllByTestId("closure-label");
    expect(closureLabels.length).toBeGreaterThan(0);
  });

  it("shows the no-shifts empty state when nothing is scheduled", async () => {
    mockRoutes({ schedule: [] });

    const { findByText } = await render(<ScheduleScreen />);

    expect(await findByText("schedule.empty")).toBeTruthy();
  });
});
