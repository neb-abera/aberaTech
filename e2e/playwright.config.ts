import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // In GitHub Actions, Playwright's built-in github reporter turns failures
  // into inline PR annotations; the list reporter keeps the log readable.
  // `make e2e` runs this suite in a container laid out so the reporter's
  // GITHUB_WORKSPACE-relative paths match the repo (e2e/<file>).
  reporter: process.env.GITHUB_ACTIONS ? [["list"], ["github"]] : "list",
  use: {
    // The production image behind `make up`, reached over the compose
    // network as app-under-test (compose.yaml says why not `app`).
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:8080",
    trace: "on-first-retry",
  },
});
