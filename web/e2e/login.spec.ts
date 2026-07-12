import { test, expect } from "./support/fixtures";

test.describe("director login", () => {
  test("valid credentials sign the director in and land on /staff", async ({ page, director }) => {
    await page.goto("/login");
    await page.getByLabel(/organisation/i).fill(director.organisationSlug);
    await page.getByLabel(/email address/i).fill(director.email);
    await page.getByLabel(/^password$/i).fill(director.password);
    await page.getByRole("button", { name: /sign in/i }).click();

    await page.waitForURL(/\/staff/);
    await expect(page.getByRole("heading", { name: "Staff" })).toBeVisible();
  });

  test("wrong password shows a generic error and does not navigate", async ({ page, director }) => {
    await page.goto("/login");
    await page.getByLabel(/organisation/i).fill(director.organisationSlug);
    await page.getByLabel(/email address/i).fill(director.email);
    await page.getByLabel(/^password$/i).fill("not-the-password");
    await page.getByRole("button", { name: /sign in/i }).click();

    await expect(page.locator('p[role="alert"]')).toHaveText("Incorrect organisation, email, or password.");
    await expect(page).toHaveURL(/\/login/);
  });

  test("unknown organisation slug shows the same generic error (no enumeration)", async ({ page, director }) => {
    await page.goto("/login");
    await page.getByLabel(/organisation/i).fill("no-such-org-slug");
    await page.getByLabel(/email address/i).fill(director.email);
    await page.getByLabel(/^password$/i).fill(director.password);
    await page.getByRole("button", { name: /sign in/i }).click();

    await expect(page.locator('p[role="alert"]')).toHaveText("Incorrect organisation, email, or password.");
    await expect(page).toHaveURL(/\/login/);
  });

  test("unknown email in a real organisation shows the same generic error", async ({ page, director }) => {
    await page.goto("/login");
    await page.getByLabel(/organisation/i).fill(director.organisationSlug);
    await page.getByLabel(/email address/i).fill("nobody@example.com");
    await page.getByLabel(/^password$/i).fill(director.password);
    await page.getByRole("button", { name: /sign in/i }).click();

    await expect(page.locator('p[role="alert"]')).toHaveText("Incorrect organisation, email, or password.");
    await expect(page).toHaveURL(/\/login/);
  });

  test("required fields block submission before any request is made", async ({ page }) => {
    await page.goto("/login");
    await page.getByRole("button", { name: /sign in/i }).click();

    // Native HTML5 required-field validation keeps the browser on the page — no server round trip.
    await expect(page).toHaveURL(/\/login/);
    const organisationField = page.getByLabel(/organisation/i);
    await expect(organisationField).toHaveJSProperty("validity.valid", false);
  });

  test("a logged-in director who reloads the app stays signed in", async ({ directorPage }) => {
    await directorPage.reload();
    await expect(directorPage).toHaveURL(/\/staff/);
    await expect(directorPage.getByRole("heading", { name: "Staff" })).toBeVisible();
  });
});
