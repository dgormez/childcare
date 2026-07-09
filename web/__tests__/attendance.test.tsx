import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import AttendancePage from "../app/(app)/attendance/page";
import { apiClient } from "../lib/apiClient";
import type { AttendanceRecordResponse, LocationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PATCH: vi.fn() },
}));

function renderAttendancePage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <AttendancePage />
    </NextIntlClientProvider>,
  );
}

function makeLocation(overrides: Partial<LocationResponse> = {}): LocationResponse {
  return {
    id: "loc-1", name: "Sunshine House", address: "1 Main St", phone: "+32 9 123 45 67",
    email: "loc@test.com", maxCapacity: 20, naamLocatie: null, dossiernummer: null,
    verantwoordelijke: null, flexPermission: false, boPermission: false, deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z", updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function makeRecord(overrides: Partial<AttendanceRecordResponse> = {}): AttendanceRecordResponse {
  return {
    id: "att-1", childId: "child-1", locationId: "loc-1", date: "2026-07-09",
    status: "present", checkInAt: "2026-07-09T07:30:00Z", checkOutAt: null,
    plannedDurationMinutes: 480, absenceJustified: null, absenceReason: null,
    recordedBy: [], createdAt: "2026-07-09T07:30:00Z", updatedAt: "2026-07-09T07:30:00Z",
    ...overrides,
  };
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.PATCH).mockReset();
});

describe("AttendancePage", () => {
  it("renders today's attendance records with the child's name and status", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/attendance") return Promise.resolve(okResponse({ items: [makeRecord()], nextCursor: null })) as never;
      if (path === "/api/children") return Promise.resolve(okResponse([{ id: "child-1", firstName: "Emma", lastName: "Peeters" }])) as never;
      return Promise.resolve(okResponse([])) as never;
    });

    renderAttendancePage();

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("Present")).toBeInTheDocument();
    expect(screen.getByText("480 min")).toBeInTheDocument();
  });

  it("shows an empty state when there are no records for the selected date", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/attendance") return Promise.resolve(okResponse({ items: [], nextCursor: null })) as never;
      return Promise.resolve(okResponse([])) as never;
    });

    renderAttendancePage();

    expect(await screen.findByText("No attendance records for this date yet.")).toBeInTheDocument();
  });

  it("opens the correction dialog and saves a check-out time", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/attendance") return Promise.resolve(okResponse({ items: [makeRecord()], nextCursor: null })) as never;
      if (path === "/api/children") return Promise.resolve(okResponse([{ id: "child-1", firstName: "Emma", lastName: "Peeters" }])) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.PATCH).mockResolvedValue(okResponse(makeRecord({ checkOutAt: "2026-07-09T17:00:00Z" })) as never);

    renderAttendancePage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Correct" }));
    expect(await screen.findByRole("dialog")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(apiClient.PATCH).toHaveBeenCalledWith(
      "/api/attendance/{id}",
      expect.objectContaining({ params: { path: { id: "att-1" } } }),
    );
  });

  it("saves a status correction to absent with absence details", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/attendance") return Promise.resolve(okResponse({ items: [makeRecord()], nextCursor: null })) as never;
      if (path === "/api/children") return Promise.resolve(okResponse([{ id: "child-1", firstName: "Emma", lastName: "Peeters" }])) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.PATCH).mockResolvedValue(okResponse(makeRecord({ status: "absent", absenceJustified: false })) as never);

    renderAttendancePage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Correct" }));
    await userEvent.selectOptions(await screen.findByRole("combobox", { name: "Status" }), "absent");
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Absence type" }), "false");
    await userEvent.type(screen.getByRole("textbox", { name: "Reason" }), "Sick");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(apiClient.PATCH).toHaveBeenCalledWith(
      "/api/attendance/{id}",
      expect.objectContaining({
        body: expect.objectContaining({
          status: "absent",
          absenceJustified: false,
          absenceReason: "Sick",
        }),
      }),
    );
  });

  it("shows a retryable error state when attendance fails to load", async () => {
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/attendance") return Promise.resolve({ response: new Response(null, { status: 500 }), data: undefined, error: {} }) as never;
      return Promise.resolve(okResponse([])) as never;
    });

    renderAttendancePage();

    expect(await screen.findByText("Couldn't load attendance. Please try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });
});
