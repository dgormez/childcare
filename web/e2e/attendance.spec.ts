import { test, expect } from "./support/fixtures";
import { seedLocation, seedGroup, seedChild, pairDevice, deviceCheckIn, type SeededDirector, type SeededChild } from "./support/seed";
import type { Page } from "@playwright/test";

async function openAttendanceWithCheckedInChild(director: SeededDirector, directorPage: Page): Promise<SeededChild> {
  const location = await seedLocation(director);
  const group = await seedGroup(director, location.id);
  const child = await seedChild(director);
  const deviceToken = await pairDevice(director, location.id, group.id);
  await deviceCheckIn(deviceToken, child.id);

  await directorPage.goto("/attendance");
  await expect(directorPage.getByText(`${child.firstName} ${child.lastName}`)).toBeVisible();
  return child;
}

test.describe("attendance", () => {
  test("a location with no records for the day shows the empty state", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.goto("/attendance");
    await expect(directorPage.getByText("No attendance records for this date yet.")).toBeVisible();
  });

  test("a checked-in child shows as present with a check-in time", async ({ director, directorPage }) => {
    const child = await openAttendanceWithCheckedInChild(director, directorPage);
    const row = directorPage.locator("tbody tr").filter({ hasText: `${child.firstName} ${child.lastName}` });
    await expect(row.getByText("Present", { exact: true })).toBeVisible();
  });

  test("correcting a check-out time on a present record saves it", async ({ director, directorPage }) => {
    const child = await openAttendanceWithCheckedInChild(director, directorPage);
    const row = directorPage.locator("tbody tr").filter({ hasText: `${child.firstName} ${child.lastName}` });
    const checkOutCell = row.locator("td").nth(3);
    await expect(checkOutCell).toHaveText("—");

    await row.getByRole("button", { name: "Correct" }).click();
    await expect(directorPage.getByText(`Correct attendance for ${child.firstName} ${child.lastName}`)).toBeVisible();
    await directorPage.getByLabel("Check-out").fill("2026-01-01T16:30");
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();

    await expect(directorPage.getByText(`Correct attendance for ${child.firstName} ${child.lastName}`)).not.toBeVisible();
    // Locale-dependent time formatting (toLocaleTimeString) means we can't assert an exact
    // string — just confirm the placeholder dash is gone, i.e. a real time got saved.
    await expect(checkOutCell).not.toHaveText("—");
  });

  test("switching a record to absent with a justification updates the status badge", async ({ director, directorPage }) => {
    const child = await openAttendanceWithCheckedInChild(director, directorPage);
    const row = directorPage.locator("tbody tr").filter({ hasText: `${child.firstName} ${child.lastName}` });

    await row.getByRole("button", { name: "Correct" }).click();
    await directorPage.getByLabel("Status").selectOption("absent");
    await directorPage.getByLabel("Reason").fill("Doctor's appointment");
    await directorPage.getByRole("button", { name: "Save", exact: true }).click();

    await expect(row.getByText("Absent", { exact: false })).toBeVisible();
    await expect(row.getByText("justified")).toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.route("**/api/attendance?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/attendance");
    await expect(directorPage.getByText("Couldn't load attendance. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/attendance?*");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No attendance records for this date yet.")).toBeVisible();
  });
});
