import os from "node:os";
import { defineConfig, devices } from "@playwright/test";

const owner = /owner\.spec\.ts$/;

const visitorProjects = [
  { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  { name: "firefox", use: { ...devices["Desktop Firefox"] } },
  { name: "webkit", use: { ...devices["Desktop Safari"] } },
  { name: "phone-chromium", use: { ...devices["Pixel 7"] } },
  { name: "phone-webkit", use: { ...devices["iPhone 15"] } },
].map((project) => ({ ...project, testIgnore: owner }));

export default defineConfig({
  testDir: ".",
  fullyParallel: true,
  // Half the cores, at most eight: three engines at 24 workers on the dev
  // box put Firefox at 15 s a test and timed the smoke suite out.
  workers: Math.min(8, Math.max(1, Math.floor(os.cpus().length / 2))),
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // In GitHub Actions, Playwright's built-in github reporter turns failures
  // into inline PR annotations; the list reporter keeps the log readable.
  // `make e2e` runs this suite in a container laid out so the reporter's
  // GITHUB_WORKSPACE-relative paths match the repo (e2e/<file>).
  reporter: process.env.GITHUB_ACTIONS ? [["list"], ["github"]] : "list",
  // Every browser engine, every run. Chromium alone passed a canvas fix on
  // 2026-09-22 that Firefox showed to be no fix at all. The Playwright image
  // `make e2e` runs in ships all three. Two phones besides, one Chromium and
  // one WebKit, because the bar, the booking dialog and the planner rail
  // each have a layout of their own under 900px.
  //
  // The owner specs (owner.spec.ts, *.owner.spec.ts) hold state on the
  // server: the in-memory dev box, the queue, the owner's documents. Two
  // engines on one of them at once would race, so they run as their own
  // projects, one engine after another, after everything else. Within an
  // engine the files run side by side: each owns different state.
  projects: [
    ...visitorProjects,
    {
      name: "owner-chromium",
      use: { ...devices["Desktop Chrome"] },
      testMatch: owner,
      dependencies: visitorProjects.map((project) => project.name),
    },
    {
      name: "owner-firefox",
      use: { ...devices["Desktop Firefox"] },
      testMatch: owner,
      dependencies: ["owner-chromium"],
    },
    {
      name: "owner-webkit",
      use: { ...devices["Desktop Safari"] },
      testMatch: owner,
      dependencies: ["owner-firefox"],
    },
  ],
  use: {
    // The production image behind `make up`, reached over the compose
    // network as app-under-test (compose.yaml says why not `app`).
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:8080",
    trace: "on-first-retry",
  },
});
