import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
    // The planner core is deliberately free of the DOM, so it needs no browser
    // environment. Component tests opt into jsdom per file, with a
    // `// @vitest-environment jsdom` line at the top.
    environment: "node",
  },
});
