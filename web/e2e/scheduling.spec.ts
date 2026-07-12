import { test, expect } from "./support/fixtures";
import { seedLocation, seedCaregiver, assignStaffLocation, type SeededDirector, type SeededCaregiver } from "./support/seed";
import type { Page } from "@playwright/test";

async function openSchedulingWithEligibleCaregiver(
  director: SeededDirector,
  directorPage: Page,
): Promise<SeededCaregiver> {
  const location = await seedLocation(director);
  const caregiver = await seedCaregiver(director);
  await assignStaffLocation(director, caregiver.id, location.id);

  await directorPage.goto("/scheduling");
  await expect(directorPage.getByText(`${caregiver.firstName} ${caregiver.lastName}`)).toBeVisible();
  // Move into a fully future week so every day is a valid shift date, regardless of what day
  // of the current week "today" happens to be.
  await directorPage.getByRole("button", { name: "Next week", exact: true }).click();

  return caregiver;
}

function staffRow(page: Page, caregiver: SeededCaregiver) {
  return page.locator("tbody tr").filter({ hasText: `${caregiver.firstName} ${caregiver.lastName}` });
}

test.describe("scheduling", () => {
  test("a location with no eligible caregivers shows the empty state", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.goto("/scheduling");
    await expect(directorPage.getByText("No shifts scheduled yet for this week.")).toBeVisible();
  });

  test("adding a shift with the default 08:00–16:00 slot saves it", async ({ director, directorPage }) => {
    const caregiver = await openSchedulingWithEligibleCaregiver(director, directorPage);

    await staffRow(directorPage, caregiver).getByRole("button", { name: "Add shift" }).first().click();
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();

    await expect(staffRow(directorPage, caregiver).getByText("08:00–16:00")).toBeVisible();
  });

  test("an overlapping shift for the same caregiver and day is rejected", async ({ director, directorPage }) => {
    const caregiver = await openSchedulingWithEligibleCaregiver(director, directorPage);
    const addButton = staffRow(directorPage, caregiver).getByRole("button", { name: "Add shift" }).first();

    await addButton.click();
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();
    await expect(staffRow(directorPage, caregiver).getByText("08:00–16:00")).toBeVisible();

    await addButton.click();
    await directorPage.getByLabel("Start time").fill("09:00");
    await directorPage.getByLabel("End time").fill("17:00");
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();

    await expect(directorPage.getByText("This staff member is already scheduled at an overlapping time.")).toBeVisible();
    await expect(staffRow(directorPage, caregiver).getByText("09:00–17:00")).not.toBeVisible();
  });

  test("marking a shift absent then removing the absence toggles the badge", async ({ director, directorPage }) => {
    const caregiver = await openSchedulingWithEligibleCaregiver(director, directorPage);
    await staffRow(directorPage, caregiver).getByRole("button", { name: "Add shift" }).first().click();
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();

    await staffRow(directorPage, caregiver).getByText("08:00–16:00").click();
    await directorPage.getByRole("button", { name: "Mark absent" }).click();
    await expect(staffRow(directorPage, caregiver).getByText("Absent")).toBeVisible();

    await staffRow(directorPage, caregiver).getByText("08:00–16:00").click();
    await directorPage.getByRole("button", { name: "Remove absence" }).click();
    await expect(staffRow(directorPage, caregiver).getByText("Absent")).not.toBeVisible();
  });

  test("deleting a shift removes it from the grid", async ({ director, directorPage }) => {
    const caregiver = await openSchedulingWithEligibleCaregiver(director, directorPage);
    await staffRow(directorPage, caregiver).getByRole("button", { name: "Add shift" }).first().click();
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();
    await expect(staffRow(directorPage, caregiver).getByText("08:00–16:00")).toBeVisible();

    await staffRow(directorPage, caregiver).getByText("08:00–16:00").click();
    await directorPage.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(staffRow(directorPage, caregiver).getByText("08:00–16:00")).not.toBeVisible();
  });

  test("copying the week duplicates the shift into the next week", async ({ director, directorPage }) => {
    const caregiver = await openSchedulingWithEligibleCaregiver(director, directorPage);
    await staffRow(directorPage, caregiver).getByRole("button", { name: "Add shift" }).first().click();
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();
    await expect(staffRow(directorPage, caregiver).getByText("08:00–16:00")).toBeVisible();

    await directorPage.getByRole("button", { name: "Copy to next week" }).click();
    await expect(directorPage.getByText("Copy week", { exact: true })).toBeVisible();
    await directorPage.getByRole("button", { name: "Copy", exact: true }).click();
    await expect(directorPage.getByText("Copy week", { exact: true })).not.toBeVisible();
    // copyWeek() fires its own reload of the *current* week asynchronously — wait for it to
    // settle before navigating, or it can race with the navigation's own reload.
    await directorPage.waitForLoadState("networkidle");

    await directorPage.getByRole("button", { name: "Next week", exact: true }).click();
    await expect(staffRow(directorPage, caregiver).getByText("08:00–16:00")).toBeVisible();
  });

  test("a failed schedule load shows an error state with a working retry", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.route("**/api/staff-schedules?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/scheduling");
    await expect(directorPage.getByText("Couldn't load the schedule. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/staff-schedules?*");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No shifts scheduled yet for this week.")).toBeVisible();
  });
});
