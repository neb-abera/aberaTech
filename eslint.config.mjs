// @ts-check

import eslint from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  eslint.configs.recommended,
  tseslint.configs.strict,
  tseslint.configs.stylistic,
  {
    ignores: ['**/node_modules/**', '**/dist/**', '**/obj/**', '**/.gitignore/**', '**../*.md'] // Add ignore patterns here
  },
  {
    // Build and maintenance scripts run under node, not in a browser, so process,
    // fetch and console are defined rather than undeclared globals.
    files: ['**/tools/**/*.{js,mjs}', '**/*.config.{js,mjs,ts}'],
    languageOptions: { globals: globals.nodeBuiltin }
  }
);
