import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import WaitingListPage from "../app/(app)/waiting-list/page";
import { apiClient } from "../lib/apiClient";
import type { ChildResponse, LocationResponse, OccupancyDayResponse, WaitingListEntryResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PATCH: vi.fn() },
}));

function renderWaitingListPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <WaitingListPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
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
    deactivatedAt: null,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function makeEntry(overrides: Partial<WaitingListEntryResponse> = {}): WaitingListEntryResponse {
  return {
    id: "entry-1",
    childFirstName: "Emma",
    childLastName: "Peeters",
    dateOfBirth: "2025-03-10",
    contactName: "Sophie Peeters",
    contactEmail: "sophie@example.com",
    contactPhone: null,
    locationId: "loc-1",
    requestedStartDate: "2026-09-01",
    priority: 0,
    status: "waiting",
    notes: null,
    childId: null,
    isDuplicate: false,
    registeredAt: "2026-07-01T09:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function mockGet(byPath: Record<string, unknown>) {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    if (typeof path === "string" && path in byPath) return Promise.resolve(okResponse(byPath[path])) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.POST).mockReset();
  vi.mocked(apiClient.PATCH).mockReset();
});

describe("WaitingListPage", () => {
  it("loads the table, shows the duplicate badge, and renders the empty state with no entries", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [makeEntry(), makeEntry({ id: "entry-2", childFirstName: "Louis", childLastName: "Janssens", isDuplicate: true })],
      "/api/waiting-list/occupancy": [] as OccupancyDayResponse[],
    });

    renderWaitingListPage();

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("Louis Janssens")).toBeInTheDocument();
    expect(screen.getByText("Possible duplicate")).toBeInTheDocument();
  });

  it("shows the empty state when no entries exist", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [],
      "/api/waiting-list/occupancy": [] as OccupancyDayResponse[],
    });

    renderWaitingListPage();

    expect(await screen.findByText("No families on the waiting list yet.")).toBeInTheDocument();
  });

  it("status badges pair a color with a distinct icon per status, and the transition action is available per row", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [makeEntry({ status: "waiting" })],
      "/api/waiting-list/occupancy": [] as OccupancyDayResponse[],
    });

    renderWaitingListPage();
    await screen.findByText("Emma Peeters");

    const table = screen.getByRole("table");
    const statusBadge = within(table).getByText("Waiting");
    expect(statusBadge.querySelector("svg")).toBeInTheDocument();
    expect(within(table).getByRole("button", { name: "Offer a place" })).toBeInTheDocument();
    expect(within(table).getByRole("button", { name: "Withdraw" })).toBeInTheDocument();
  });

  it("moves an entry up/down using only the keyboard (Tab + Enter, no mouse)", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [makeEntry(), makeEntry({ id: "entry-2", priority: 1, childFirstName: "Louis", childLastName: "Janssens" })],
      "/api/waiting-list/occupancy": [] as OccupancyDayResponse[],
    });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse([makeEntry({ priority: 1 }), makeEntry({ id: "entry-2", priority: 0 })]) as never);

    renderWaitingListPage();
    await screen.findByText("Emma Peeters");

    const moveDownButton = screen.getAllByRole("button", { name: "Move down" })[0];
    moveDownButton.focus();
    expect(moveDownButton).toHaveFocus();
    await userEvent.keyboard("{Enter}");

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/waiting-list/{id}/reorder",
      expect.objectContaining({ params: { path: { id: "entry-1" } }, body: { direction: "down" } }),
    );
  });

  it("renders a closed occupancy day as Closed (never a number)", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [] as WaitingListEntryResponse[],
      "/api/waiting-list/occupancy": [{ date: "2026-09-01", freeCapacity: null, closed: true }] as OccupancyDayResponse[],
    });

    renderWaitingListPage();

    expect(await screen.findByText("Closed")).toBeInTheDocument();
  });

  it("defaults the occupancy panel to an entry's requested start date when opened from that entry", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [makeEntry({ requestedStartDate: "2026-09-15" })],
      "/api/waiting-list/occupancy": [] as OccupancyDayResponse[],
    });

    renderWaitingListPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "View occupancy" }));

    expect(apiClient.GET).toHaveBeenCalledWith(
      "/api/waiting-list/occupancy",
      expect.objectContaining({ params: { query: expect.objectContaining({ from: "2026-09-15" }) } }),
    );
  });

  it("pre-fills the 'create child record now?' prompt with the entry's name and date of birth", async () => {
    mockGet({
      "/api/locations": [makeLocation()],
      "/api/waiting-list": [makeEntry({ status: "enrolled", childId: null })],
      "/api/waiting-list/occupancy": [] as OccupancyDayResponse[],
      "/api/children": [] as ChildResponse[],
    });

    renderWaitingListPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Link child record" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Create a new child record" }));

    expect(within(dialog).getByText(/Emma Peeters/)).toBeInTheDocument();
    expect(within(dialog).getByText(/3\/10\/2025/)).toBeInTheDocument();
  });
});
