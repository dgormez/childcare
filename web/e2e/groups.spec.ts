import { test, expect } from "./support/fixtures";
import { seedLocation, seedGroup, pairDevice, seedGroupActivity, type SeededDirector } from "./support/seed";
import type { Page } from "@playwright/test";

async function openGroupsForSeededGroup(director: SeededDirector, directorPage: Page) {
  const location = await seedLocation(director);
  const group = await seedGroup(director, location.id);
  const deviceToken = await pairDevice(director, location.id, group.id);
  await directorPage.goto("/groups");
  return { location, group, deviceToken };
}

test.describe("groups", () => {
  test("a freshly registered org shows the empty state", async ({ director, directorPage }) => {
    await openGroupsForSeededGroup(director, directorPage);
    await expect(directorPage.getByText("No events or activities yet for this group and date.")).toBeVisible();
  });

  test("an activity logged by a caregiver appears on the timeline and can be deleted", async ({ director, directorPage }) => {
    const { deviceToken } = await openGroupsForSeededGroup(director, directorPage);
    await seedGroupActivity(deviceToken, "Nature walk", "Outdoor");
    await directorPage.reload();

    await expect(directorPage.getByText("Nature walk")).toBeVisible();

    await directorPage.getByRole("button", { name: "Delete" }).click();
    await expect(directorPage.getByText("Delete activity?")).toBeVisible();
    await directorPage.getByRole("button", { name: "Delete", exact: true }).last().click();

    await expect(directorPage.getByText("Nature walk", { exact: true })).not.toBeVisible();
    await expect(directorPage.getByText("No events or activities yet for this group and date.")).toBeVisible();
  });

  test("a failed timeline load shows an error state with a working retry", async ({ director, directorPage }) => {
    await directorPage.route("**/api/group-activities/director-timeline?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );
    await openGroupsForSeededGroup(director, directorPage);

    await expect(directorPage.getByText("Could not load the timeline.")).toBeVisible();

    await directorPage.unroute("**/api/group-activities/director-timeline?*");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No events or activities yet for this group and date.")).toBeVisible();
  });
});
