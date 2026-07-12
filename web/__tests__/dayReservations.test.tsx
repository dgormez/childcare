import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import DayReservationsPage from "../app/(app)/requests/page";
import { apiClient } from "../lib/apiClient";
import type { DayReservationResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn() },
}));

function renderPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <DayReservationsPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function errorResponse(errorKey: string, status = 409) {
  return { response: new Response(null, { status }), data: undefined, error: { errorKey } };
}

function makeReservation(overrides: Partial<DayReservationResponse> = {}): DayReservationResponse {
  return {
    id: "res-1",
    childId: "child-1",
    childDisplayName: "Emma Peeters",
    type: "absence",
    requestedDate: "2026-07-13",
    exchangeForDate: null,
    reason: "Koorts",
    absenceJustified: null,
    status: "pending",
    requestedBy: "user-1",
    decidedBy: null,
    decidedAt: null,
    directorNotes: null,
    capacityWarning: null,
    createdAt: "2026-07-11T09:00:00Z",
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
});

describe("DayReservationsPage", () => {
  it("renders the pending queue with child, type, date, and reason", async () => {
    mockGet({ "/api/day-reservations": [makeReservation()] });

    renderPage();

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("Absence")).toBeInTheDocument();
    expect(screen.getByText("Koorts")).toBeInTheDocument();
  });

  it("shows the empty state when no requests are pending", async () => {
    mockGet({ "/api/day-reservations": [] });

    renderPage();

    expect(await screen.findByText("No pending requests.")).toBeInTheDocument();
  });

  it("shows a capacity warning badge for an extra-day request at/over capacity", async () => {
    mockGet({
      "/api/day-reservations": [makeReservation({ id: "res-2", type: "extra", capacityWarning: true, reason: null })],
    });

    renderPage();

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("This location may be at or over capacity on this date.")).toBeInTheDocument();
  });

  it("approving an absence request sends the selected justified flag", async () => {
    mockGet({ "/api/day-reservations": [makeReservation()] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeReservation({ status: "approved", absenceJustified: true })) as never);

    renderPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Approve" }));
    await userEvent.click(screen.getByRole("button", { name: "Confirm approval" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/day-reservations/{id}/approve",
      expect.objectContaining({ params: { path: { id: "res-1" } }, body: { absenceJustified: true } }),
    );
  });

  it("rejecting a request sends the entered director note", async () => {
    mockGet({ "/api/day-reservations": [makeReservation()] });
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse(makeReservation({ status: "rejected", directorNotes: "Te laat" })) as never);

    renderPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Reject" }));
    await userEvent.type(screen.getByLabelText("Note (optional)"), "Te laat");
    await userEvent.click(screen.getByRole("button", { name: "Confirm rejection" }));

    expect(apiClient.POST).toHaveBeenCalledWith(
      "/api/day-reservations/{id}/reject",
      expect.objectContaining({ params: { path: { id: "res-1" } }, body: { directorNotes: "Te laat" } }),
    );
  });

  it("surfaces a clear message when approving fails because the date became a closure day", async () => {
    mockGet({ "/api/day-reservations": [makeReservation()] });
    vi.mocked(apiClient.POST).mockResolvedValue(errorResponse("errors.day_reservations.closure_day") as never);

    renderPage();
    await screen.findByText("Emma Peeters");

    await userEvent.click(screen.getByRole("button", { name: "Approve" }));
    await userEvent.click(screen.getByRole("button", { name: "Confirm approval" }));

    expect(await screen.findByText("This date has become a closure day — this absence can't be approved.")).toBeInTheDocument();
  });

  // Feature 013f: a null decidedBy on an approved row is a system auto-approval (informational
  // mode), distinguished from a director decision — and shouldn't offer approve/reject actions.
  it("shows an auto-approved badge and no actions for a system-decided row, but not for a director-decided one", async () => {
    mockGet({
      "/api/day-reservations": [
        makeReservation({ id: "auto", childDisplayName: "Emma Peeters", status: "approved", decidedBy: null, decidedAt: "2026-07-11T09:00:00Z" }),
        makeReservation({ id: "manual", childDisplayName: "Liam Janssens", status: "approved", decidedBy: "director-1", decidedAt: "2026-07-11T09:00:00Z" }),
      ],
    });

    renderPage();
    await screen.findByText("Emma Peeters");
    await screen.findByText("Liam Janssens");

    expect(screen.getAllByText("Auto-approved")).toHaveLength(1);
    expect(screen.queryByRole("button", { name: "Approve" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
  });
});
