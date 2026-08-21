import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
    // The planner core is deliberately free of the DOM, so it needs no browser
    // environment. Add jsdom here if a component test is ever added.
    environment: 'node'
  }
});
