import { test as base, type Page } from "@playwright/test";
import { seedDirector, type SeededDirector } from "./seed";

async function loginAsDirector(page: Page, director: SeededDirector) {
  await page.goto("/login");
  await page.getByLabel(/organisation/i).fill(director.organisationSlug);
  await page.getByLabel(/email address/i).fill(director.email);
  await page.getByLabel(/^password$/i).fill(director.password);
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL(/\/staff/);
}

interface Fixtures {
  /** A freshly seeded org + director, independent of any UI state. */
  director: SeededDirector;
  /** A page already logged in as `director` and sitting on the post-login /staff route. */
  directorPage: Page;
}

export const test = base.extend<Fixtures>({
  director: async ({}, use) => {
    await use(await seedDirector());
  },

  directorPage: async ({ page, director }, use) => {
    await loginAsDirector(page, director);
    await use(page);
  },
});

export { expect } from "@playwright/test";
export { loginAsDirector };
