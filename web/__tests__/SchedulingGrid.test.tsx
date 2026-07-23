import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "../i18n/locales/en.json";
import { SchedulingGrid } from "../components/SchedulingGrid";
import type { StaffResponse, StaffScheduleResponse } from "../lib/types";

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
    contractedDays: [],
    timeEntryFunctions: [],
    ...overrides,
  };
}

// A fixed Monday-first week: 2026-07-13 (Mon) .. 2026-07-19 (Sun).
const weekDates = ["2026-07-13", "2026-07-14", "2026-07-15", "2026-07-16", "2026-07-17", "2026-07-18", "2026-07-19"];

function renderGrid(overrides: {
  staff?: StaffResponse[];
  entries?: StaffScheduleResponse[];
  closureDates?: Set<string>;
} = {}) {
  const { staff = [makeStaff()], entries = [], closureDates = new Set<string>() } = overrides;
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <SchedulingGrid
        weekDates={weekDates}
        staff={staff}
        entries={entries}
        groupsById={new Map()}
        projectedOnDutyByDate={new Map()}
        closureDates={closureDates}
        onAddShift={vi.fn()}
        onSelectShift={vi.fn()}
      />
    </NextIntlClientProvider>,
  );
}

describe("SchedulingGrid", () => {
  it("renders a normal (contracted, open) day's cell as interactive", () => {
    renderGrid();
    // 2026-07-13 is a Monday; with no ContractedDays restriction every day is schedulable.
    expect(screen.getAllByRole("button", { name: "Add shift" }).length).toBe(weekDates.length);
  });

  it("greys a day outside the staff member's contracted days and hides the add-shift action (FR-002)", () => {
    // Only Monday/Tuesday contracted — every other day's cell must be non-interactive.
    const staff = [makeStaff({ contractedDays: ["Monday", "Tuesday"] })];
    renderGrid({ staff });

    const nonContractedCell = screen.getByTestId(`grid-cell-${staff[0].id}-2026-07-15`); // Wednesday
    expect(nonContractedCell.querySelector('button[aria-label="Add shift"]')).toBeNull();
    expect(nonContractedCell.textContent).toContain("Not contracted");

    const contractedCell = screen.getByTestId(`grid-cell-${staff[0].id}-2026-07-13`); // Monday
    expect(contractedCell.querySelector('button[aria-label="Add shift"]')).not.toBeNull();
  });

  it("greys an entire closure-day column for every staff member and hides the add-shift action (FR-002)", () => {
    const staffA = makeStaff({ id: "staff-a" });
    const staffB = makeStaff({ id: "staff-b", firstName: "Bram" });
    renderGrid({ staff: [staffA, staffB], closureDates: new Set(["2026-07-16"]) }); // Thursday

    for (const member of [staffA, staffB]) {
      const closedCell = screen.getByTestId(`grid-cell-${member.id}-2026-07-16`);
      expect(closedCell.querySelector('button[aria-label="Add shift"]')).toBeNull();
      expect(closedCell.textContent).toContain("Closed");
    }

    const headers = screen.getAllByTestId("grid-column-header-2026-07-16");
    expect(headers[0].textContent).toContain("Closed");

    // A non-closed column stays interactive.
    const openCell = screen.getByTestId(`grid-cell-${staffA.id}-2026-07-13`);
    expect(openCell.querySelector('button[aria-label="Add shift"]')).not.toBeNull();
  });
});
