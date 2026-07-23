import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { TimeEntryCorrectionDialog } from "../components/staff/TimeEntryCorrectionDialog";
import { apiClient } from "../lib/apiClient";
import type { StaffTimeEntryResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), POST: vi.fn(), PATCH: vi.fn() },
}));

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

const openEntry: StaffTimeEntryResponse = {
  id: "entry-1",
  staffProfileId: "staff-1",
  locationId: "loc-1",
  groupId: null,
  clockedInAt: "2026-07-20T08:00:00Z",
  clockedOutAt: null,
  function: "kinderbegeleider",
  notes: null,
  isOpen: true,
  isLocked: false,
  unlockedAt: null,
};

const lockedEntry: StaffTimeEntryResponse = {
  ...openEntry,
  id: "entry-2",
  clockedOutAt: "2026-07-01T16:00:00Z",
  isOpen: false,
  isLocked: true,
};

function renderDialog(entry: StaffTimeEntryResponse, onSaved = vi.fn()) {
  render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <TimeEntryCorrectionDialog entry={entry} onOpenChange={vi.fn()} onSaved={onSaved} />
    </NextIntlClientProvider>,
  );
  return onSaved;
}

beforeEach(() => {
  vi.mocked(apiClient.PATCH).mockReset();
  vi.mocked(apiClient.POST).mockReset();
});

describe("TimeEntryCorrectionDialog", () => {
  it("shows the correction form and saves without a warning when no overlap", async () => {
    vi.mocked(apiClient.PATCH).mockResolvedValue(okResponse({ overlapWarning: false }) as never);
    const onSaved = renderDialog(openEntry);

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(apiClient.PATCH).toHaveBeenCalled());
    expect(onSaved).toHaveBeenCalled();
    expect(screen.queryByText(/overlaps another time entry/)).not.toBeInTheDocument();
  });

  it("surfaces the overlap warning instead of closing when the server flags one", async () => {
    vi.mocked(apiClient.PATCH).mockResolvedValue(okResponse({ overlapWarning: true }) as never);
    const onSaved = renderDialog(openEntry);

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText(/overlaps another time entry/)).toBeInTheDocument();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("shows a locked message and only an unlock action for a locked entry", async () => {
    renderDialog(lockedEntry);

    expect(screen.getByText(/more than 7 days old and locked/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Unlock" })).toBeInTheDocument();
  });

  it("calls the unlock endpoint when Unlock is pressed", async () => {
    vi.mocked(apiClient.POST).mockResolvedValue(okResponse({}) as never);
    const onSaved = renderDialog(lockedEntry);

    await userEvent.click(screen.getByRole("button", { name: "Unlock" }));

    await waitFor(() =>
      expect(apiClient.POST).toHaveBeenCalledWith("/api/staff-time-entries/{id}/unlock", { params: { path: { id: "entry-2" } } }),
    );
    expect(onSaved).toHaveBeenCalled();
  });
});
