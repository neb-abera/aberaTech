// @vitest-environment jsdom
/**
 * The theme serves both schemes through CSS variables, with dark as the
 * default. A style override that reads `theme.palette.*` directly bakes the
 * dark default's literal colour into BOTH schemes — white button text on the
 * light background, an opaque dark app bar over a light page. Overrides must
 * emit the variable, so the browser resolves the active scheme's colour.
 */

import Button from "@mui/material/Button";
import IconButton from "@mui/material/IconButton";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, beforeAll, describe, expect, it } from "vitest";
import TechnicalTransitionGuide from "../../views/TechnicalTransitionGuide";
import AppTheme from "../AppTheme";

afterEach(cleanup);

// Same rehearsal as PlannerBoard.mobile: the worker's first MUI mount and
// first getComputedStyle each pay a one-time emotion/jsdom cost that under CI
// load blows the first test's 5s budget. The guide page is the heavy mount
// here, so it is the one rehearsed.
beforeAll(() => {
  render(
    <MemoryRouter>
      <AppTheme>
        <TechnicalTransitionGuide />
      </AppTheme>
    </MemoryRouter>,
  );
  getComputedStyle(screen.getByText(/the target audience for this/i));
  cleanup();
}, 30_000);

describe("scheme-aware overrides", () => {
  it("gives outlined buttons the active scheme’s text colour, not the dark default’s", () => {
    render(
      <AppTheme>
        <Button variant="outlined">9:00 AM</Button>
      </AppTheme>,
    );

    expect(getComputedStyle(screen.getByRole("button")).color).toBe(
      "var(--template-palette-text-primary)",
    );
  });

  it("gives icon buttons the active scheme’s text colour, not the dark default’s", () => {
    render(
      <AppTheme>
        <IconButton aria-label="Instagram" />
      </AppTheme>,
    );

    expect(getComputedStyle(screen.getByRole("button")).color).toBe(
      "var(--template-palette-text-primary)",
    );
  });

  it("paints the guide pages’ boxes from the active scheme, not the dark default", () => {
    render(
      <MemoryRouter>
        <AppTheme>
          <TechnicalTransitionGuide />
        </AppTheme>
      </MemoryRouter>,
    );

    const box = screen.getByText(/the target audience for this/i)
      .parentElement as HTMLElement;
    expect(getComputedStyle(box).backgroundColor).toBe(
      "var(--template-palette-background-paper)",
    );
    expect(getComputedStyle(box).color).toBe(
      "var(--template-palette-text-primary)",
    );
  });
});
