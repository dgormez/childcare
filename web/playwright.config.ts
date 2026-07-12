import { defineConfig, devices } from "@playwright/test";
import { existsSync, readFileSync } from "fs";

// Loads web/.env.e2e (gitignored, see .env.e2e.example) so E2E_SUPERADMIN_API_KEY etc. don't
// need to be exported by hand every run. No `dotenv` dependency needed for a handful of vars.
if (existsSync(".env.e2e")) {
  for (const line of readFileSync(".env.e2e", "utf-8").split("\n")) {
    const match = line.match(/^\s*([\w.-]+)\s*=\s*(.*)?\s*$/);
    if (match && !process.env[match[1]]) process.env[match[1]] = match[2]?.trim() ?? "";
  }
}

const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  timeout: 30_000,

  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },

  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],

  // Assumes the backend API (dotnet run) and Postgres (docker compose up) are already running —
  // E2E tests seed real data through the API, so there's no in-memory backend to spin up here.
  webServer: {
    command: "npm run dev",
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
});
