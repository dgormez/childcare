import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import IncidentsPage from "../app/(app)/incidents/page";
import IncidentDetailPage from "../app/(app)/incidents/[id]/page";
import { apiClient } from "../lib/apiClient";
import type { IncidentReportResponse, PagedIncidentReportsResponse } from "../lib/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  useParams: () => ({ id: "report-1" }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn(), PUT: vi.fn() },
  getAccessToken: () => "test-token",
}));

function renderComponent(ui: React.ReactElement) {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      {ui}
    </NextIntlClientProvider>,
  );
}

function okResponse(data: unknown) {
  return { response: new Response(null, { status: 200 }), data, error: undefined };
}

function makeReport(overrides: Partial<IncidentReportResponse> = {}): IncidentReportResponse {
  return {
    id: "report-1",
    childId: "child-1",
    locationId: "loc-1",
    occurredAt: "2026-07-12T09:14:00Z",
    locationDetail: "outdoor",
    description: "Scraped knee on the playground.",
    injuryType: "scrape",
    firstAidGiven: "Cleaned and bandaged",
    doctorCalled: false,
    doctorNotes: null,
    parentNotified: true,
    parentNotifiedAt: "2026-07-12T09:20:00Z",
    parentNotifiedHow: "phone",
    reportedBy: ["staff-1"],
    witnesses: null,
    followUp: null,
    reviewedAt: null,
    createdAt: "2026-07-12T09:14:32Z",
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
  vi.mocked(apiClient.PUT).mockReset();
  push.mockReset();
});

describe("IncidentsPage (list)", () => {
  it("renders rows with an unreviewed indicator for an unreviewed report", async () => {
    mockGet({
      "/api/children": [{ id: "child-1", firstName: "Emma", lastName: "Peeters" }],
      "/api/locations": [{ id: "loc-1", name: "Location A" }],
      "/api/incident-reports": { items: [makeReport()], page: 1, pageSize: 25, totalCount: 1 } satisfies PagedIncidentReportsResponse,
    });

    renderComponent(<IncidentsPage />);

    const table = await screen.findByRole("table");
    expect(within(table).getByText("Emma Peeters")).toBeInTheDocument();
    expect(within(table).getByText("Location A")).toBeInTheDocument();
    expect(within(table).getByText("Unreviewed")).toBeInTheDocument();
  });

  it("shows a reviewed indicator for a reviewed report", async () => {
    mockGet({
      "/api/children": [{ id: "child-1", firstName: "Emma", lastName: "Peeters" }],
      "/api/locations": [{ id: "loc-1", name: "Location A" }],
      "/api/incident-reports": {
        items: [makeReport({ reviewedAt: "2026-07-12T10:00:00Z" })], page: 1, pageSize: 25, totalCount: 1,
      } satisfies PagedIncidentReportsResponse,
    });

    renderComponent(<IncidentsPage />);

    const table = await screen.findByRole("table");
    expect(within(table).getByText("Emma Peeters")).toBeInTheDocument();
    expect(within(table).getByText("Reviewed")).toBeInTheDocument();
  });

  // Convergence T062/FR-009: a result set larger than one page must expose a way to reach
  // page 2+ — otherwise those reports are permanently unreachable through the UI.
  it("shows working pagination when there are more reports than fit on one page", async () => {
    mockGet({
      "/api/children": [{ id: "child-1", firstName: "Emma", lastName: "Peeters" }],
      "/api/locations": [{ id: "loc-1", name: "Location A" }],
      "/api/incident-reports": { items: [makeReport()], page: 1, pageSize: 25, totalCount: 30 } satisfies PagedIncidentReportsResponse,
    });

    renderComponent(<IncidentsPage />);
    await screen.findByRole("table");

    expect(screen.getByText("Page 1 of 2")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Previous/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Next/ })).toBeEnabled();

    vi.mocked(apiClient.GET).mockClear();
    mockGet({
      "/api/children": [{ id: "child-1", firstName: "Emma", lastName: "Peeters" }],
      "/api/locations": [{ id: "loc-1", name: "Location A" }],
      "/api/incident-reports": { items: [makeReport({ id: "report-2" })], page: 2, pageSize: 25, totalCount: 30 } satisfies PagedIncidentReportsResponse,
    });
    await userEvent.click(screen.getByRole("button", { name: /Next/ }));

    expect(apiClient.GET).toHaveBeenCalledWith(
      "/api/incident-reports",
      expect.objectContaining({ params: expect.objectContaining({ query: expect.objectContaining({ page: 2 }) }) }),
    );
    expect(await screen.findByText("Page 2 of 2")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Next/ })).toBeDisabled();
  });

  it("shows the empty state when no incidents match", async () => {
    mockGet({
      "/api/children": [],
      "/api/locations": [],
      "/api/incident-reports": { items: [], page: 1, pageSize: 25, totalCount: 0 } satisfies PagedIncidentReportsResponse,
    });

    renderComponent(<IncidentsPage />);

    expect(await screen.findByText("No incident reports found.")).toBeInTheDocument();
  });

  it("applying the child filter re-fetches with the selected childId", async () => {
    mockGet({
      "/api/children": [
        { id: "child-1", firstName: "Emma", lastName: "Peeters" },
        { id: "child-2", firstName: "Liam", lastName: "Janssens" },
      ],
      "/api/locations": [{ id: "loc-1", name: "Location A" }],
      "/api/incident-reports": { items: [makeReport()], page: 1, pageSize: 25, totalCount: 1 } satisfies PagedIncidentReportsResponse,
    });

    renderComponent(<IncidentsPage />);
    await screen.findByRole("table");
    vi.mocked(apiClient.GET).mockClear();

    await userEvent.selectOptions(screen.getByLabelText("Child"), "child-2");

    expect(apiClient.GET).toHaveBeenCalledWith(
      "/api/incident-reports",
      expect.objectContaining({ params: expect.objectContaining({ query: expect.objectContaining({ childId: "child-2" }) }) }),
    );
  });
});

describe("IncidentDetailPage", () => {
  it("opening the detail view reads the reviewed state the GET call already resolved (reviewed-on-open is server-side)", async () => {
    mockGet({ "/api/incident-reports/{id}": makeReport({ reviewedAt: "2026-07-12T10:00:00Z" }) });

    renderComponent(<IncidentDetailPage />);

    expect(await screen.findByText("Scraped knee on the playground.")).toBeInTheDocument();
    expect(apiClient.GET).toHaveBeenCalledWith(
      "/api/incident-reports/{id}",
      expect.objectContaining({ params: { path: { id: "report-1" } } }),
    );
  });

  // T053: a follow-up note saves on a locked (>24h) report while every other field renders
  // read-only — this detail page never exposes any other field as an editable input at all
  // (web-side editing scope is the follow-up note only; full-field correction is API-level,
  // used by the mobile same-window path), so "read-only" holds unconditionally.
  it("saves a follow-up note on a report older than 24 hours", async () => {
    const oldReport = makeReport({
      createdAt: new Date(Date.now() - 48 * 60 * 60 * 1000).toISOString(),
      reviewedAt: "2026-07-12T10:00:00Z",
    });
    mockGet({ "/api/incident-reports/{id}": oldReport });
    vi.mocked(apiClient.PUT).mockResolvedValue(okResponse({ ...oldReport, followUp: "Doctor visit follow-up." }) as never);

    renderComponent(<IncidentDetailPage />);
    await screen.findByText("Scraped knee on the playground.");

    expect(screen.getByText("This report is more than 24 hours old — only the follow-up note can still be edited.")).toBeInTheDocument();

    await userEvent.type(screen.getByPlaceholderText("Add a follow-up note (e.g. a later doctor visit)"), "Doctor visit follow-up.");
    await userEvent.click(screen.getByRole("button", { name: "Save follow-up" }));

    expect(apiClient.PUT).toHaveBeenCalledWith(
      "/api/incident-reports/{id}",
      expect.objectContaining({
        params: { path: { id: "report-1" } },
        body: expect.objectContaining({ followUp: "Doctor visit follow-up." }),
      }),
    );
    expect(await screen.findByText("Follow-up note saved.")).toBeInTheDocument();
  });
});
