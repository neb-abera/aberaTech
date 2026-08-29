// @vitest-environment jsdom
/**
 * The theme serves both schemes through CSS variables, with dark as the
 * default. A style override that reads `theme.palette.*` directly bakes the
 * dark default's literal colour into BOTH schemes — white button text on the
 * light background, an opaque dark app bar over a light page. Overrides must
 * emit the variable, so the browser resolves the active scheme's colour.
 */
import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import AppTheme from '../AppTheme';

afterEach(cleanup);

describe('scheme-aware overrides', () => {
  it('gives outlined buttons the active scheme’s text colour, not the dark default’s', () => {
    render(
      <AppTheme>
        <Button variant="outlined">9:00 AM</Button>
      </AppTheme>
    );

    expect(getComputedStyle(screen.getByRole('button')).color).toBe('var(--template-palette-text-primary)');
  });

  it('gives icon buttons the active scheme’s text colour, not the dark default’s', () => {
    render(
      <AppTheme>
        <IconButton aria-label="Instagram" />
      </AppTheme>
    );

    expect(getComputedStyle(screen.getByRole('button')).color).toBe('var(--template-palette-text-primary)');
  });
});
