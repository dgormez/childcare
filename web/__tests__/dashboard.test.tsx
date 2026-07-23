import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import DashboardPage from "../app/(app)/dashboard/page";
import { apiClient } from "../lib/apiClient";
import type {
  VaccinationsDueSoonResponse,
  OccupancySummaryResponse,
  BkrRatioOverviewResponse,
  BkrBreachHistoryResponse,
  AttendanceSummaryResponse,
  InvoiceStatusOverviewResponse,
  DataCompletenessResponse,
} from "../lib/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("../lib/apiClient", () => ({
  apiClient: { GET: vi.fn() },
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

const emptyOccupancy: OccupancySummaryResponse = { asOf: "2026-07-18", locations: [] };
const emptyBkr: BkrRatioOverviewResponse = { asOf: "2026-07-18T10:00:00Z", groups: [] };
const emptyBreaches: BkrBreachHistoryResponse = { from: "2026-06-18", to: "2026-07-18", breaches: [] };
const emptyAttendance: AttendanceSummaryResponse = { month: "2026-07-01", children: [], groupTotals: [], locationTotals: [] };
const emptyInvoices: InvoiceStatusOverviewResponse = {
  month: "2026-07-01", paidCount: 0, paidTotalCents: 0, outstandingCount: 0, outstandingTotalCents: 0,
  overdueCount: 0, overdueTotalCents: 0, totalInvoicedCents: 0, overdueInvoices: [],
};
const emptyCompleteness: DataCompletenessResponse = { flags: [] };

function mockAllEndpoints(overrides: Partial<Record<string, unknown>> = {}) {
  vi.mocked(apiClient.GET).mockImplementation((path: unknown) => {
    const key = String(path);
    if (key in overrides) return Promise.resolve(okResponse(overrides[key])) as never;
    if (key === "/api/locations") return Promise.resolve(okResponse([])) as never;
    if (key === "/api/vaccine-records/due-soon") return Promise.resolve(okResponse([])) as never;
    if (key === "/api/staff/contracts-expiring") return Promise.resolve(okResponse([])) as never;
    if (key === "/api/reports/occupancy") return Promise.resolve(okResponse(emptyOccupancy)) as never;
    if (key === "/api/reports/bkr") return Promise.resolve(okResponse(emptyBkr)) as never;
    if (key === "/api/reports/bkr/breaches") return Promise.resolve(okResponse(emptyBreaches)) as never;
    if (key === "/api/reports/attendance-summary") return Promise.resolve(okResponse(emptyAttendance)) as never;
    if (key === "/api/reports/invoices") return Promise.resolve(okResponse(emptyInvoices)) as never;
    if (key === "/api/reports/data-completeness") return Promise.resolve(okResponse(emptyCompleteness)) as never;
    return Promise.resolve(okResponse([])) as never;
  });
}

beforeEach(() => {
  vi.mocked(apiClient.GET).mockReset();
  push.mockReset();
});

describe("DashboardPage — due-soon block", () => {
  it("renders overdue and due-soon rows sorted as returned by the backend", async () => {
    const items: VaccinationsDueSoonResponse[] = [
      { childId: "child-1", childName: "Emma Peeters", locationId: "loc-1", vaccineName: "Hep B", nextDueDate: "2026-07-05", isOverdue: true },
      { childId: "child-2", childName: "Louis Janssens", locationId: "loc-1", vaccineName: "MMR", nextDueDate: "2026-07-20", isOverdue: false },
    ];
    mockAllEndpoints({ "/api/vaccine-records/due-soon": items });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Emma Peeters — Hep B")).toBeInTheDocument();
    expect(screen.getByText("Louis Janssens — MMR")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getByText("Due soon")).toBeInTheDocument();
  });

  it("shows a calm empty state when nothing is due", async () => {
    mockAllEndpoints();
    renderComponent(<DashboardPage />);
    expect(await screen.findByText("No vaccinations due or overdue.")).toBeInTheDocument();
  });
});

describe("DashboardPage — occupancy section", () => {
  it("renders green/amber/red using the icon mapping FR-018 specifies, and a clean 0/capacity state", async () => {
    const occupancy: OccupancySummaryResponse = {
      asOf: "2026-07-18",
      locations: [
        {
          locationId: "loc-1", locationName: "Main site", presentCount: 0, capacity: 20, status: "green",
          groups: [
            { groupId: "g1", groupName: "Babies", presentCount: 5, capacity: 10, status: "green" },
            { groupId: "g2", groupName: "Toddlers", presentCount: 9, capacity: 8, status: "red" },
            { groupId: "g3", groupName: "No capacity set", presentCount: 3, capacity: null, status: null },
          ],
          weekAhead: [],
        },
      ],
    };
    mockAllEndpoints({ "/api/reports/occupancy": occupancy });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("0 / 20")).toBeInTheDocument();
    expect(screen.getByText("5 / 10")).toBeInTheDocument();
    expect(screen.getByText("9 / 8")).toBeInTheDocument();
    expect(screen.getAllByText("Within capacity").length).toBeGreaterThan(0);
    expect(screen.getByText("Over capacity")).toBeInTheDocument();
    // Group with no capacity set shows a plain headcount, no status badge.
    expect(screen.getByText("3")).toBeInTheDocument();

    // Clicking a group row navigates to its existing screen (convergence finding F1).
    await userEvent.click(screen.getByText("Toddlers"));
    expect(push).toHaveBeenCalledWith("/groups");
  });
});

describe("DashboardPage — BKR compliance section", () => {
  it("shows the live ratio per group and an empty breach-history state", async () => {
    const bkr: BkrRatioOverviewResponse = {
      asOf: "2026-07-18T10:00:00Z",
      groups: [
        { groupId: "g1", locationId: "loc-1", presentCount: 9, qualifiedStaffCount: 1, isNapTime: false, threshold: 8, status: "red" },
      ],
    };
    mockAllEndpoints({ "/api/reports/bkr": bkr });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Present: 9 — Qualified staff: 1")).toBeInTheDocument();
    expect(screen.getByText("No breaches in this period.")).toBeInTheDocument();

    // Clicking the breaching group's row navigates to its existing screen (convergence finding F1).
    await userEvent.click(screen.getByText("Present: 9 — Qualified staff: 1"));
    expect(push).toHaveBeenCalledWith("/groups");
  });

  it("renders a known breach window's start and end", async () => {
    const breaches: BkrBreachHistoryResponse = {
      from: "2026-06-18",
      to: "2026-07-18",
      breaches: [{ groupId: "g1", locationId: "loc-1", startedAt: "2026-07-10T13:05:00Z", endedAt: "2026-07-10T13:40:00Z" }],
    };
    mockAllEndpoints({ "/api/reports/bkr/breaches": breaches });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText((text) => text.startsWith("Started:"))).toBeInTheDocument();
  });
});

describe("DashboardPage — attendance summary section", () => {
  it("renders totals per child", async () => {
    const summary: AttendanceSummaryResponse = {
      month: "2026-06-01",
      children: [
        { childId: "c1", childName: "Emma Peeters", groupId: "g1", locationId: "loc-1", presentDays: 18, absentJustifiedDays: 1, absentUnjustifiedDays: 0, closureDays: 2 },
      ],
      groupTotals: [],
      locationTotals: [],
    };
    mockAllEndpoints({ "/api/reports/attendance-summary": summary });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("18")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Export CSV" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Export PDF" })).toBeInTheDocument();
  });
});

describe("DashboardPage — invoice status section", () => {
  it("renders buckets and the overdue list", async () => {
    const invoices: InvoiceStatusOverviewResponse = {
      month: "2026-07-01", paidCount: 40, paidTotalCents: 2400000, outstandingCount: 5, outstandingTotalCents: 300000,
      overdueCount: 1, overdueTotalCents: 60000, totalInvoicedCents: 2760000,
      overdueInvoices: [{ invoiceId: "inv-1", childName: "Louis Janssens", dueDate: "2026-07-01", daysOverdue: 17, totalCents: 60000 }],
    };
    mockAllEndpoints({ "/api/reports/invoices": invoices });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Louis Janssens")).toBeInTheDocument();
    expect(screen.getByText("17 days overdue")).toBeInTheDocument();
  });

  it("shows a calm empty state when there are no overdue invoices", async () => {
    mockAllEndpoints();
    renderComponent(<DashboardPage />);
    expect(await screen.findByText("No overdue invoices.")).toBeInTheDocument();
  });
});

describe("DashboardPage — data completeness section", () => {
  it("flags all four gap types with a link to the affected record", async () => {
    const completeness: DataCompletenessResponse = {
      flags: [
        { type: "missing_pickup_contact", subjectType: "child", subjectId: "c1", subjectName: "Emma Peeters", detail: null },
        { type: "overdue_vaccine", subjectType: "child", subjectId: "c2", subjectName: "Louis Janssens", detail: "DTP (due 2026-05-01)" },
        { type: "missing_qualification", subjectType: "staff", subjectId: "s1", subjectName: "Anna Staff", detail: null },
        { type: "missing_pin", subjectType: "staff", subjectId: "s2", subjectName: "Ben Staff", detail: null },
      ],
    };
    mockAllEndpoints({ "/api/reports/data-completeness": completeness });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Emma Peeters")).toBeInTheDocument();
    expect(screen.getByText("No authorised pickup contact")).toBeInTheDocument();
    expect(screen.getByText("Overdue vaccine — DTP (due 2026-05-01)")).toBeInTheDocument();
    expect(screen.getByText("Missing qualification level")).toBeInTheDocument();
    expect(screen.getByText("No check-in PIN set")).toBeInTheDocument();
  });

  it("shows a calm empty state when nothing is flagged", async () => {
    mockAllEndpoints();
    renderComponent(<DashboardPage />);
    expect(await screen.findByText("Nothing to flag.")).toBeInTheDocument();
  });

  // Feature 022 FR-007
  it("flags a child with no recorded identity verification", async () => {
    const completeness: DataCompletenessResponse = {
      flags: [
        { type: "missing_identity_verification", subjectType: "child", subjectId: "c3", subjectName: "Nora Peeters", detail: null },
      ],
    };
    mockAllEndpoints({ "/api/reports/data-completeness": completeness });

    renderComponent(<DashboardPage />);

    expect(await screen.findByText("Nora Peeters")).toBeInTheDocument();
    expect(screen.getByText("Identity not yet verified")).toBeInTheDocument();
  });
});
