import { configDefaults, defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
    // Vitest's default include glob (`**/*.{test,spec}.ts`) also matches Playwright's own
    // e2e/*.spec.ts suite (playwright.config.ts's testDir) — without this exclusion Vitest
    // tries to execute them itself and fails with "did not expect test.describe() to be called
    // here" (a runner conflict, not a real test failure). Pre-existing gap, unrelated to this
    // feature — found while running the full web suite for feature 013b.
    exclude: [...configDefaults.exclude, "e2e/**"],
  },
});
