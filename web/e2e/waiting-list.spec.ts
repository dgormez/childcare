import { test, expect } from "./support/fixtures";
import { seedLocation, type SeededDirector } from "./support/seed";
import type { Page } from "@playwright/test";

async function openWaitingListForSeededLocation(director: SeededDirector, directorPage: Page) {
  await seedLocation(director);
  await directorPage.goto("/waiting-list");
  await expect(directorPage.getByText("No families on the waiting list yet.")).toBeVisible();
}

async function addEntry(
  page: Page,
  child: { firstName: string; lastName: string; dob: string; contactName: string },
) {
  await page.getByRole("button", { name: "Add to waiting list" }).click();
  await page.getByLabel("Child's first name").fill(child.firstName);
  await page.getByLabel("Child's last name").fill(child.lastName);
  await page.getByLabel("Date of birth").fill(child.dob);
  await page.getByLabel("Contact name").fill(child.contactName);
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(page.getByText(`${child.firstName} ${child.lastName}`)).toBeVisible();
}

test.describe("waiting list", () => {
  test("adding an entry with the required fields lists it as waiting", async ({ director, directorPage }) => {
    await openWaitingListForSeededLocation(director, directorPage);

    await addEntry(directorPage, { firstName: "Nora", lastName: "Nolet", dob: "2023-02-14", contactName: "Pat Nolet" });

    const row = directorPage.locator("tbody tr").filter({ hasText: "Nora Nolet" });
    await expect(row.getByText("Waiting", { exact: true })).toBeVisible();
  });

  test("Save stays disabled until every required field is filled", async ({ director, directorPage }) => {
    await openWaitingListForSeededLocation(director, directorPage);

    await directorPage.getByRole("button", { name: "Add to waiting list" }).click();
    const save = directorPage.getByRole("button", { name: "Save", exact: true });
    await expect(save).toBeDisabled();

    await directorPage.getByLabel("Child's first name").fill("Nora");
    await directorPage.getByLabel("Child's last name").fill("Nolet");
    await expect(save).toBeDisabled();

    await directorPage.getByLabel("Date of birth").fill("2023-02-14");
    await directorPage.getByLabel("Contact name").fill("Pat Nolet");
    await expect(save).toBeEnabled();
  });

  test("moving an entry down reorders it below the next one", async ({ director, directorPage }) => {
    await openWaitingListForSeededLocation(director, directorPage);
    await addEntry(directorPage, { firstName: "Alpha", lastName: "First", dob: "2023-01-01", contactName: "A Contact" });
    await addEntry(directorPage, { firstName: "Beta", lastName: "Second", dob: "2023-01-02", contactName: "B Contact" });

    const rows = directorPage.locator("tbody tr");
    await expect(rows.nth(0)).toContainText("Alpha First");
    await expect(rows.nth(1)).toContainText("Beta Second");

    await rows.nth(0).getByRole("button", { name: "Move down" }).click();

    await expect(rows.nth(0)).toContainText("Beta Second");
    await expect(rows.nth(1)).toContainText("Alpha First");
  });

  test("offering then enrolling a waiting entry moves it through both statuses", async ({ director, directorPage }) => {
    await openWaitingListForSeededLocation(director, directorPage);
    await addEntry(directorPage, { firstName: "Nora", lastName: "Nolet", dob: "2023-02-14", contactName: "Pat Nolet" });
    // The list defaults to filtering by status=waiting, so an offered/enrolled entry would drop
    // out of view on the next reload unless we widen the filter first.
    await directorPage.getByLabel("Status").selectOption("all");

    const row = directorPage.locator("tbody tr").filter({ hasText: "Nora Nolet" });
    await row.getByRole("button", { name: "Offer a place" }).click();
    await expect(row.getByText("Offered", { exact: true })).toBeVisible();

    await row.getByRole("button", { name: "Enroll" }).click();
    await expect(row.getByText("Enrolled", { exact: true })).toBeVisible();
  });

  test("withdrawing a waiting entry marks it withdrawn", async ({ director, directorPage }) => {
    await openWaitingListForSeededLocation(director, directorPage);
    await addEntry(directorPage, { firstName: "Nora", lastName: "Nolet", dob: "2023-02-14", contactName: "Pat Nolet" });
    await directorPage.getByLabel("Status").selectOption("all");

    const row = directorPage.locator("tbody tr").filter({ hasText: "Nora Nolet" });
    await row.getByRole("button", { name: "Withdraw" }).click();
    await expect(row.getByText("Withdrawn", { exact: true })).toBeVisible();
  });

  test("enrolling an entry then creating a new child record clears the link-child action", async ({ director, directorPage }) => {
    await openWaitingListForSeededLocation(director, directorPage);
    await addEntry(directorPage, { firstName: "Nora", lastName: "Nolet", dob: "2023-02-14", contactName: "Pat Nolet" });
    await directorPage.getByLabel("Status").selectOption("all");

    const row = directorPage.locator("tbody tr").filter({ hasText: "Nora Nolet" });
    await row.getByRole("button", { name: "Offer a place" }).click();
    await row.getByRole("button", { name: "Enroll" }).click();
    await expect(row.getByRole("button", { name: "Link child record" })).toBeVisible();

    await row.getByRole("button", { name: "Link child record" }).click();
    await expect(directorPage.getByRole("heading", { name: "Link child record" })).toBeVisible();
    await directorPage.getByRole("button", { name: "Link", exact: true }).click();

    await expect(row.getByRole("button", { name: "Link child record" })).not.toBeVisible();
  });

  test("a failed list load shows an error state with a working retry", async ({ director, directorPage }) => {
    await seedLocation(director);
    await directorPage.route("**/api/waiting-list?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await directorPage.goto("/waiting-list");
    await expect(directorPage.getByText("Couldn't load the waiting list. Please try again.")).toBeVisible();

    await directorPage.unroute("**/api/waiting-list?*");
    await directorPage.getByRole("button", { name: "Retry" }).click();
    await expect(directorPage.getByText("No families on the waiting list yet.")).toBeVisible();
  });
});
