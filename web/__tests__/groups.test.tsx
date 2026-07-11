import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import GroupsPage from "../app/(app)/groups/page";
import { apiClient } from "../lib/apiClient";
import type { GroupTimelineResponse, LocationResponse, GroupResponse } from "../lib/types";

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), DELETE: vi.fn() },
}));

function renderGroupsPage() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <GroupsPage />
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

const location: LocationResponse = { id: "loc-1", name: "Sunshine House" } as LocationResponse;
const group: GroupResponse = { id: "group-1", name: "Ducklings", locationId: "loc-1" };

const timelineWithActivity: GroupTimelineResponse = {
  entries: [
    {
      kind: "group_activity",
      occurredAt: "2026-07-11T09:00:00.000Z",
      childEvent: null,
      groupActivity: {
        id: "activity-1",
        groupId: "group-1",
        activityType: "outdoor",
        title: "In de tuin",
        description: "Buiten gespeeld",
        occurredAt: "2026-07-11T09:00:00.000Z",
        recordedBy: [],
        photos: [],
        createdAt: "2026-07-11T09:00:00.000Z",
      },
    },
  ],
};

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  vi.mocked(apiClient.DELETE).mockReset();
});

function mockDefaultGets(timeline: GroupTimelineResponse) {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    if (path === "/api/locations") return Promise.resolve(okResponse([location]) as never);
    if (path === "/api/groups") return Promise.resolve(okResponse([group]) as never);
    if (path === "/api/group-activities/director-timeline") return Promise.resolve(okResponse(timeline) as never);
    return Promise.resolve(okResponse({}) as never);
  });
}

describe("GroupsPage", () => {
  it("renders the merged timeline with a group activity", async () => {
    mockDefaultGets(timelineWithActivity);

    renderGroupsPage();

    expect(await screen.findByText("In de tuin")).toBeInTheDocument();
    expect(screen.getByText("Buiten gespeeld")).toBeInTheDocument();
  });

  it("deletes an activity after confirmation", async () => {
    mockDefaultGets(timelineWithActivity);
    vi.mocked(apiClient.DELETE).mockResolvedValue(okResponse({}) as never);

    renderGroupsPage();
    await screen.findByText("In de tuin");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(apiClient.DELETE).toHaveBeenCalledWith(
      "/api/group-activities/{id}",
      expect.objectContaining({ params: { path: { id: "activity-1" } } }),
    );
  });

  it("lets the director change the date via the date picker", async () => {
    mockDefaultGets(timelineWithActivity);

    renderGroupsPage();
    await screen.findByText("In de tuin");

    const dateInput = screen.getByLabelText("Date") as HTMLInputElement;
    await userEvent.clear(dateInput);
    await userEvent.type(dateInput, "2026-07-01");

    expect(apiClient.GET).toHaveBeenCalledWith(
      "/api/group-activities/director-timeline",
      expect.objectContaining({ params: { query: { groupId: "group-1", date: "2026-07-01" } } }),
    );
  });

  it("shows an empty state when there are no events or activities", async () => {
    mockDefaultGets({ entries: [] });

    renderGroupsPage();

    expect(await screen.findByText("No events or activities yet for this group and date.")).toBeInTheDocument();
  });
});
