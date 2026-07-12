import { test, expect } from "./support/fixtures";
import { seedLocation, type SeededDirector } from "./support/seed";
import type { Page } from "@playwright/test";

function daysFromNow(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

async function openClosuresForSeededLocation(director: SeededDirector, directorPage: Page) {
  const location = await seedLocation(director);
  await directorPage.goto("/closures");
  await expect(directorPage.getByText("No closure days for this location and year yet.")).toBeVisible();
  return location;
}

test.describe("closures", () => {
  test("adding a training-day closure with a future date creates a draft", async ({ director, directorPage }) => {
    await openClosuresForSeededLocation(director, directorPage);

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(daysFromNow(10));
    await directorPage.getByLabel("Label").fill("Staff Training Day");
    await directorPage.getByLabel("Type").selectOption("training");
    await directorPage.getByRole("button", { name: "Save" }).click();

    await expect(directorPage.getByText("Staff Training Day")).toBeVisible();
    await expect(directorPage.getByText("Draft")).toBeVisible();
  });

  test("a past date is rejected with a clear error and no closure is created", async ({ director, directorPage }) => {
    await openClosuresForSeededLocation(director, directorPage);

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(daysFromNow(-3));
    await directorPage.getByLabel("Label").fill("Backdated Closure");
    await directorPage.getByRole("button", { name: "Save" }).click();

    await expect(directorPage.getByText("Closure days can only be created for today or a future date.")).toBeVisible();
    await expect(directorPage.getByText("No closure days for this location and year yet.")).toBeVisible();
  });

  test("a second closure on an already-used date is rejected as a duplicate", async ({ director, directorPage }) => {
    await openClosuresForSeededLocation(director, directorPage);
    const date = daysFromNow(15);

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(date);
    await directorPage.getByLabel("Label").fill("First Closure");
    await directorPage.getByRole("button", { name: "Save" }).click();
    await expect(directorPage.getByText("First Closure")).toBeVisible();

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(date);
    await directorPage.getByLabel("Label").fill("Second Closure");
    await directorPage.getByRole("button", { name: "Save" }).click();

    await expect(directorPage.getByText("This location already has a closure on that date.")).toBeVisible();
    await expect(directorPage.getByText("Second Closure")).not.toBeVisible();
  });

  test("publishing a draft closure notifies and marks it published", async ({ director, directorPage }) => {
    await openClosuresForSeededLocation(director, directorPage);

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(daysFromNow(20));
    await directorPage.getByLabel("Label").fill("Publish Me");
    await directorPage.getByRole("button", { name: "Save" }).click();
    await expect(directorPage.getByText("Publish Me")).toBeVisible();

    await directorPage.getByRole("button", { name: "Publish", exact: true }).click();
    await expect(directorPage.getByText("Publish closure")).toBeVisible();
    await directorPage.getByRole("button", { name: "Publish", exact: true }).last().click();

    await expect(directorPage.getByText(/parent messages created/)).toBeVisible();
    await expect(directorPage.getByText("Published")).toBeVisible();
    await expect(directorPage.getByRole("button", { name: "Edit" })).not.toBeVisible();
  });

  test("cancelling a published closure notifies parents and marks it cancelled", async ({ director, directorPage }) => {
    await openClosuresForSeededLocation(director, directorPage);

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(daysFromNow(25));
    await directorPage.getByLabel("Label").fill("Cancel Me");
    await directorPage.getByRole("button", { name: "Save" }).click();
    await directorPage.getByRole("button", { name: "Publish", exact: true }).click();
    await directorPage.getByRole("button", { name: "Publish", exact: true }).last().click();
    await expect(directorPage.getByText("Published")).toBeVisible();

    await directorPage.getByRole("button", { name: "Cancel closure" }).click();
    await expect(directorPage.getByText("Cancel published closure")).toBeVisible();
    await directorPage.getByRole("button", { name: "Cancel closure", exact: true }).last().click();

    await expect(directorPage.getByText(/cancellation messages created/)).toBeVisible();
    await expect(directorPage.getByText("Cancelled")).toBeVisible();
    await expect(directorPage.getByRole("button", { name: "Cancel closure" })).not.toBeVisible();
  });

  test("removing a draft closure deletes it outright", async ({ director, directorPage }) => {
    await openClosuresForSeededLocation(director, directorPage);

    await directorPage.getByRole("button", { name: "Add closure" }).click();
    await directorPage.locator('input[type="date"]').fill(daysFromNow(30));
    await directorPage.getByLabel("Label").fill("Remove Me");
    await directorPage.getByRole("button", { name: "Save" }).click();
    await expect(directorPage.getByText("Remove Me")).toBeVisible();

    await directorPage.getByRole("button", { name: "Remove", exact: true }).click();
    await expect(directorPage.getByText("Remove draft closure")).toBeVisible();
    await directorPage.getByRole("button", { name: "Remove", exact: true }).last().click();

    await expect(directorPage.getByText("Draft closure removed.")).toBeVisible();
    await expect(directorPage.getByText("No closure days for this location and year yet.")).toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.route("**/api/closures?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/closures");
    await expect(directorPage.getByText("Couldn't load closure days. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/closures?*");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No closure days for this location and year yet.")).toBeVisible();
  });
});
