import { test, expect } from "./support/fixtures";
import { seedChild, seedParent, seedLocation, seedActiveContract, submitDayReservation, type SeededDirector, type SeededChild } from "./support/seed";

// Contracts only run Mon–Fri (CreateContractCommandValidator), and approval matches the
// requested date's weekday against the contract's contracted days — so the seeded date must
// land on a weekday, at least `minDaysAhead` out to clear notice-period/past-date checks.
function nextWeekday(minDaysAhead: number): string {
  const d = new Date();
  d.setDate(d.getDate() + minDaysAhead);
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

async function seedPendingAbsenceRequest(director: SeededDirector): Promise<SeededChild> {
  const location = await seedLocation(director);
  const child = await seedChild(director);
  await seedActiveContract(director, child.id, location.id);
  const parent = await seedParent(director, child.id);
  await submitDayReservation(parent, child.id, nextWeekday(5), "Absence");
  return child;
}

test.describe("requests", () => {
  test("a freshly registered org shows the empty state", async ({ directorPage }) => {
    await directorPage.goto("/requests");
    await expect(directorPage.getByText("No pending requests.")).toBeVisible();
  });

  test("approving a pending absence request as justified marks it approved", async ({ director, directorPage }) => {
    const child = await seedPendingAbsenceRequest(director);
    await directorPage.goto("/requests");

    const row = directorPage.locator("tbody tr").filter({ hasText: child.firstName });
    await expect(row.getByText("Absence", { exact: true })).toBeVisible();

    await row.getByRole("button", { name: "Approve" }).click();
    await expect(directorPage.getByText("Approve request")).toBeVisible();
    await directorPage.getByRole("button", { name: "Justified", exact: true }).click();
    await directorPage.getByRole("button", { name: "Confirm approval" }).click();

    // Approved rows drop out of the default "pending" filter, same as scheduling/waiting-list.
    await expect(row).not.toBeVisible();
    await directorPage.getByLabel("Status").selectOption("all");
    // DayReservationsTable has no Status column (see KNOWN_GAPS.md) — the only observable
    // difference for a decided row is that its Approve/Reject actions are gone.
    const allViewRow = directorPage.locator("tbody tr").filter({ hasText: child.firstName });
    await expect(allViewRow).toBeVisible();
    await expect(allViewRow.getByRole("button", { name: "Approve" })).not.toBeVisible();
    await expect(allViewRow.getByRole("button", { name: "Reject" })).not.toBeVisible();
  });

  test("rejecting a pending request with a note marks it rejected", async ({ director, directorPage }) => {
    const child = await seedPendingAbsenceRequest(director);
    await directorPage.goto("/requests");

    const row = directorPage.locator("tbody tr").filter({ hasText: child.firstName });
    await row.getByRole("button", { name: "Reject" }).click();
    await expect(directorPage.getByText("Reject request")).toBeVisible();
    await directorPage.getByLabel("Note (optional)").fill("Please resubmit with more notice.");
    await directorPage.getByRole("button", { name: "Confirm rejection" }).click();

    await expect(row).not.toBeVisible();
    await directorPage.getByLabel("Status").selectOption("all");
    const allViewRow = directorPage.locator("tbody tr").filter({ hasText: child.firstName });
    await expect(allViewRow).toBeVisible();
    await expect(allViewRow.getByRole("button", { name: "Approve" })).not.toBeVisible();
    await expect(allViewRow.getByRole("button", { name: "Reject" })).not.toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ directorPage }) => {
    await directorPage.route("**/api/day-reservations?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/requests");
    await expect(directorPage.getByText("Couldn't load requests. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/day-reservations?*");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No pending requests.")).toBeVisible();
  });
});
