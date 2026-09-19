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
      reporter: ["text", "text-summary"],
      // Measured 2026-09-19: 66.7% lines, 66.6% statements, 46.1% branches,
      // 57.1% functions (1220 tests; 57.5% lines on 2026-08 when the lines
      // floor was set at 50). Each floor sits five to ten points below its
      // measurement, so a reasonable refactor doesn't break the build while
      // deleting tests or landing a large untested feature does. A ratchet
      // to raise as coverage grows, not a target.
      thresholds: {
        lines: 55,
        statements: 60,
        branches: 40,
        functions: 50,
      },
    },
  },
});
