import { test, expect } from "./support/fixtures";
import { seedCaregiver } from "./support/seed";

// No "Add Staff" UI exists yet (see KNOWN_GAPS.md) — caregivers are seeded directly through the
// API, same as a director would create them, and these specs cover everything the UI *does*
// expose: search, PIN reset, deactivate/reactivate, and the loading/error/empty states.
const API_BASE_URL = process.env.E2E_API_BASE_URL ?? "http://localhost:5001";

test.describe("staff", () => {
  test("a freshly registered org shows the empty state", async ({ directorPage }) => {
    await expect(directorPage.getByText("No staff members yet.")).toBeVisible();
  });

  test("searching filters the staff list by name", async ({ director, directorPage }) => {
    const alex = await seedCaregiver(director);
    const jamie = await seedCaregiver(director);
    await directorPage.reload();

    await expect(directorPage.getByText(`${alex.firstName} ${alex.lastName}`)).toBeVisible();
    await expect(directorPage.getByText(`${jamie.firstName} ${jamie.lastName}`)).toBeVisible();

    await directorPage.getByPlaceholder("Search by name…").fill(alex.lastName);
    await expect(directorPage.getByText(`${alex.firstName} ${alex.lastName}`)).toBeVisible();
    await expect(directorPage.getByText(`${jamie.firstName} ${jamie.lastName}`)).not.toBeVisible();
  });

  test("resetting a PIN with a valid 4-digit code succeeds", async ({ director, directorPage }) => {
    const caregiver = await seedCaregiver(director);
    await directorPage.reload();

    const row = directorPage.getByRole("row", { name: new RegExp(caregiver.lastName) });
    await row.getByRole("button", { name: "Reset PIN" }).click();
    await directorPage.getByLabel("4-digit PIN").fill("4321");
    await directorPage.getByRole("button", { name: "Save PIN" }).click();

    await expect(directorPage.getByText("Reset PIN for")).not.toBeVisible();
  });

  test("resetting a PIN with fewer than 4 digits shows a validation error", async ({ director, directorPage }) => {
    const caregiver = await seedCaregiver(director);
    await directorPage.reload();

    const row = directorPage.getByRole("row", { name: new RegExp(caregiver.lastName) });
    await row.getByRole("button", { name: "Reset PIN" }).click();
    await directorPage.getByLabel("4-digit PIN").fill("12");
    await directorPage.getByRole("button", { name: "Save PIN" }).click();

    await expect(directorPage.getByRole("alert")).toHaveText("PIN must be exactly 4 digits.");
  });

  test("deactivating then reactivating a caregiver updates their status", async ({ director, directorPage }) => {
    const caregiver = await seedCaregiver(director);
    await directorPage.reload();

    const row = directorPage.getByRole("row", { name: new RegExp(caregiver.lastName) });
    await expect(row.getByText("Active")).toBeVisible();

    await row.getByRole("button", { name: "Deactivate" }).click();
    await expect(directorPage.getByText("Deactivate staff member")).toBeVisible();
    await directorPage.getByRole("button", { name: "Deactivate", exact: true }).last().click();

    await expect(row.getByText("Deactivated")).toBeVisible();

    await row.getByRole("button", { name: "Reactivate" }).click();
    await directorPage.getByRole("button", { name: "Reactivate", exact: true }).last().click();
    await expect(row.getByText("Active")).toBeVisible();
  });

  test("a failed load shows an error state with a working retry", async ({ director, page }) => {
    // Fails every GET while routed, not just the first — React's dev-mode Strict Mode
    // double-invokes effects, so a "fail once then pass" toggle races with itself.
    await page.route("**/api/staff?*", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: "{}" }),
    );

    await page.goto("/login");
    await page.getByLabel(/organisation/i).fill(director.organisationSlug);
    await page.getByLabel(/email address/i).fill(director.email);
    await page.getByLabel(/^password$/i).fill(director.password);
    await page.getByRole("button", { name: /sign in/i }).click();
    await page.waitForURL(/\/staff/);

    await expect(page.getByText("Couldn't load staff. Please try again.")).toBeVisible();

    await page.unroute("**/api/staff?*");
    await page.getByRole("button", { name: "Retry" }).click();
    await expect(page.getByText("No staff members yet.")).toBeVisible();
  });
});
