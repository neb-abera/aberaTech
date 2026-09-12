/**
 * Runs before every test file.
 *
 * Testing Library's waitFor gives up after one second by default. That is
 * calibrated for an unloaded browser, not for a jsdom worker paying v8
 * coverage instrumentation while other files share the machine: the
 * planner's hover test missed it twice with nothing about the board
 * changed. The budget is set once here, the way vitest.config.ts sets
 * testTimeout, rather than per call where the next flake would need its
 * own. A real hang still fails, ten seconds later.
 *
 * Only in a DOM environment: the planner's core tests run in node, where
 * there is nothing to configure.
 */
if (typeof document !== "undefined") {
  const { configure } = await import("@testing-library/dom");
  configure({ asyncUtilTimeout: 10_000 });
}
