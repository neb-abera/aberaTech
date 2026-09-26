// @vitest-environment jsdom
/**
 * Switching between light and dark repaints without animating every colour
 * transition on the page, and does it without an inline style element. MUI's
 * own disableTransitionOnChange writes a <style> with text into the head,
 * which the CSP refuses (e2e/csp.spec.ts). The page's stylesheet holds the
 * rule instead, and the switch toggles a class on the root for one tick.
 */

import { useColorScheme } from "@mui/material/styles";
import { act, cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import AppTheme, { SWITCHING_CLASS } from "../AppTheme";

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  document.documentElement.classList.remove(SWITCHING_CLASS);
});

let setMode: (mode: "light" | "dark") => void = () => undefined;

function Switch() {
  setMode = useColorScheme().setMode as typeof setMode;
  return null;
}

describe("switching the colour scheme", () => {
  it("holds transitions off for one tick with a class, not an inline style", () => {
    vi.useFakeTimers();
    render(
      <AppTheme>
        <Switch />
      </AppTheme>,
    );
    const stylesBefore = document.head.querySelectorAll(
      "style:not([data-emotion])",
    ).length;
    expect(document.documentElement.classList.contains(SWITCHING_CLASS)).toBe(
      false,
    );

    act(() => setMode("light"));

    expect(document.documentElement.classList.contains(SWITCHING_CLASS)).toBe(
      true,
    );
    expect(
      document.head.querySelectorAll("style:not([data-emotion])").length,
    ).toBe(stylesBefore);

    act(() => {
      vi.runAllTimers();
    });
    expect(document.documentElement.classList.contains(SWITCHING_CLASS)).toBe(
      false,
    );
  });
});
