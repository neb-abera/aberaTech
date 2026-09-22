import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
    // The planner core is deliberately free of the DOM, so it needs no browser
    // environment. Component tests opt into jsdom per file, with a
    // `// @vitest-environment jsdom` line at the top.
    environment: "node",
    // 5s (the default) is calibrated for uninstrumented runs; with --coverage
    // every render and query pays v8 instrumentation overhead on top, and the
    // heavy MUI component tests (already warm-cache rehearsed in beforeAll)
    // drift past 5s under CI load. Still tight enough to catch a real hang.
    testTimeout: 15_000,
    // waitFor's own budget lives in src/test/setup.ts, for the same reason.
    setupFiles: ["./src/test/setup.ts"],
    // Activated by `vitest run --coverage` (the Dockerfile's clienttest stage
    // and therefore CI); `make test` stays fast without it. The thresholds
    // fail the run on their own, and sit below the measured value so a
    // reasonable refactor doesn't break the build while a change landing
    // meaningful untested logic does.
    coverage: {
      provider: "v8",
      // Code files only: a bare src/** also feeds READMEs to the parser.
      include: ["src/**/*.ts", "src/**/*.tsx"],
      exclude: ["src/**/*.test.ts", "src/**/*.test.tsx", "src/**/*.d.ts"],
      // text for the log (client-checks.yml lifts the summary lines into
      // the job summary), lcov for Codecov.
      reporter: ["text", "text-summary", "lcov"],
      // Measured 2026-09-19: 66.7% lines, 66.6% statements, 46.1% branches,
      // 57.1% functions (1220 tests; 57.5% lines on 2026-08 when the lines
      // floor was set at 50). Measured 2026-09-22: 68.6% lines, 49.2%
      // branches (58 files), when the floors moved to 62/62/44/54. Each
      // floor sits five to ten points below its measurement, so a
      // reasonable refactor doesn't break the build while deleting tests or
      // landing a large untested feature does. A ratchet to raise as
      // coverage grows, not a target.
      thresholds: {
        lines: 62,
        statements: 62,
        branches: 44,
        functions: 54,
      },
    },
  },
});
