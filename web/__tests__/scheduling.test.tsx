import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import SchedulingPage from "../app/(app)/scheduling/page";
import { apiClient } from "../lib/apiClient";
import type { GroupResponse, LocationResponse, StaffResponse, StaffScheduleResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PATCH: vi.fn(), DELETE: vi.fn() },
}));

function renderSchedulingPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <SchedulingPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(status: number, errorKey: string) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey } };
}

function makeLocation(overrides: Partial<LocationResponse> = {}): LocationResponse {
  return {
    id: "loc-1",
    name: "Sunshine House",
    address: "1 Main St",
    phone: "+32 9 123 45 67",
    email: "loc@test.com",
    maxCapacity: 20,
    naamLocatie: null,
    dossiernummer: null,
    verantwoordelijke: null,
    flexPermission: false,
    boPermission: false,
    reservationAbsencesMode: "approval",
    reservationExtrasMode: "approval",
    reservationSwapsMode: "disabled",
    reservationNoticeHours: 0,
    requiresCaregiverPin: true,
    menuVariantPriorityOrder: [],
    menuVariantsWithPublishedContent: [],
    erkenningsnummer: null,
    bankAccountNumber: null,
    invoiceDueDays: 14,
    deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function makeStaff(overrides: Partial<StaffResponse> = {}): StaffResponse {
  return {
    id: "staff-1",
    tenantUserId: "user-1",
    firstName: "Marie",
    lastName: "Peeters",
    email: "marie@test.com",
    phone: "+32 9 123 45 67",
    role: "staff",
    qualificationLevel: "QualifiedCaregiver",
    photoDownloadUrl: null,
    eligibleLocationIds: ["loc-1"],
    deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function makeGroup(overrides: Partial<GroupResponse> = {}): GroupResponse {
  return { id: "group-1", name: "Baby Room", locationId: "loc-1", ...overrides };
}

function makeEntry(overrides: Partial<StaffScheduleResponse> = {}): StaffScheduleResponse {
  return {
    id: "entry-1",
    staffProfileId: "staff-1",
    locationId: "loc-1",
    groupId: "group-1",
    date: new Date().toISOString().slice(0, 10),
    startTime: "08:00:00",
    endTime: "16:00:00",
    isAbsent: false,
    absenceReason: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function mockGet(overrides: { staff?: StaffResponse[]; entries?: StaffScheduleResponse[]; projectedCount?: number } = {}) {
  const { staff = [makeStaff()], entries = [], projectedCount = 1 } = overrides;
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
    if (path === "/api/staff") return Promise.resolve(okResponse(staff)) as never;
    if (path === "/api/groups") return Promise.resolve(okResponse([makeGroup()])) as never;
    if (path === "/api/staff-schedules") return Promise.resolve(okResponse(entries)) as never;
    if (path === "/api/staff-schedules/projected-on-duty") return Promise.resolve(okResponse({ projectedOnDutyCount: projectedCount, staffProfileIds: [] })) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.PATCH).mockReset();
  vi.mocked(apiClient.DELETE).mockReset();
});

describe("SchedulingPage", () => {
  it("renders the week grid with a scheduled shift", async () => {
    mockGet({ entries: [makeEntry()] });

    renderSchedulingPage();

    expect(await screen.findByText("Marie Peeters")).toBeInTheDocument();
    expect(screen.getByText("08:00–16:00")).toBeInTheDocument();
    expect(screen.getByText("Baby Room")).toBeInTheDocument();
  });

  it("shows an empty state when there is no staff to schedule", async () => {
    mockGet({ staff: [] });

    renderSchedulingPage();

    expect(await screen.findByText("No shifts scheduled yet for this week.")).toBeInTheDocument();
  });

  it("surfaces an overlap error on the conflicting cell without closing the dialog", async () => {
    mockGet({ entries: [makeEntry()] });
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse(409, "errors.staff_schedules.overlap") as never);

    renderSchedulingPage();
    await screen.findByText("Marie Peeters");

    await userEvent.click(screen.getAllByRole("button", { name: "Add shift" })[1]);
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await screen.findByText("This staff member is already scheduled at an overlapping time.")).toBeInTheDocument();
  });

  it("shows the skipped-entries summary after copying the week", async () => {
    mockGet({ entries: [makeEntry()] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({
      copiedCount: 0,
      skipped: [{ date: "2026-07-20", staffProfileId: "staff-1", reason: "closure_day" }],
    }) as never);

    renderSchedulingPage();
    await screen.findByText("Marie Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Copy to next week" }));
    const confirmDialog = await screen.findByRole("dialog");
    await userEvent.click(within(confirmDialog).getByRole("button", { name: "Copy" }));

    expect(await screen.findByText("1 shift(s) skipped")).toBeInTheDocument();
  });

  it("marks a shift absent and updates the badge and projected on-duty indicator", async () => {
    // Dynamic mock state: the absence POST flips both the entry's isAbsent flag and the
    // projected-on-duty count the subsequent reload (`load()`) picks up — proves the UI
    // actually reflects the server response, not just that the request was sent.
    let currentEntries = [makeEntry()];
    let currentProjectedCount = 1;
    vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
      if (path === "/api/locations") return Promise.resolve(okResponse([makeLocation()])) as never;
      if (path === "/api/staff") return Promise.resolve(okResponse([makeStaff()])) as never;
      if (path === "/api/groups") return Promise.resolve(okResponse([makeGroup()])) as never;
      if (path === "/api/staff-schedules") return Promise.resolve(okResponse(currentEntries)) as never;
      if (path === "/api/staff-schedules/projected-on-duty") return Promise.resolve(okResponse({ projectedOnDutyCount: currentProjectedCount, staffProfileIds: [] })) as never;
      return Promise.resolve(okResponse([])) as never;
    });
    vi.mocked(apiClient.POST).mockImplementation((path: unknown) => {
      if (path === "/api/staff-schedules/{id}/absence") {
        currentEntries = [makeEntry({ isAbsent: true, absenceReason: "sick" })];
        currentProjectedCount = 0;
        return Promise.resolve(okResponse(currentEntries[0])) as never;
      }
      return Promise.resolve(okResponse({})) as never;
    });

    renderSchedulingPage();
    await screen.findByText("Marie Peeters");
    expect(screen.getAllByText("Projected on duty: 1").length).toBeGreaterThan(0);

    await userEvent.click(screen.getByText("08:00–16:00"));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Mark absent" }));

    await waitFor(() => expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/staff-schedules/{id}/absence",
      expect.objectContaining({ params: { path: { id: "entry-1" } }, body: { isAbsent: true, absenceReason: "sick" } }),
    ));
    expect(await screen.findByText("Absent")).toBeInTheDocument();
    expect(screen.getAllByText("Projected on duty: 0").length).toBeGreaterThan(0);
  });
});
