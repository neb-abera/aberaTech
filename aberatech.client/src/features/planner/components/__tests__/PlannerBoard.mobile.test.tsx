// @vitest-environment jsdom
/**
 * On a phone the settings rail is a pane at the top of the board that expands
 * in place, not an overlay. The pane collapsed is the default, so the plan is
 * the first thing seen; expanding it reveals the same rail the wide layout
 * keeps in its left column.
 */
import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import AppTheme from '../../../../theme/AppTheme';
import PlannerBoard from '../PlannerBoard';

beforeAll(() => {
  // jsdom has no matchMedia. Answering no to every query makes
  // useMediaQuery(breakpoints.up('md')) report a narrow screen.
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false
    }) as MediaQueryList;
});

// Without vitest globals, testing-library cannot register its own cleanup.
afterEach(cleanup);

function mount() {
  return render(
    <AppTheme>
      <PlannerBoard />
    </AppTheme>
  );
}

describe('the rail on a narrow screen', () => {
  it('is a collapsed pane that expands in place, not an overlay', () => {
    mount();

    const summary = screen.getByRole('button', { name: /tracks, focus areas and settings/i });
    expect(summary.getAttribute('aria-expanded')).toBe('false');

    fireEvent.click(summary);

    expect(summary.getAttribute('aria-expanded')).toBe('true');
    // In place means in the page: nothing modal appears over the board.
    expect(document.querySelector('.MuiDrawer-root')).toBeNull();
    expect(document.querySelector('.MuiModal-root')).toBeNull();
  });

  it('shows the rail itself once expanded', () => {
    mount();

    fireEvent.click(screen.getByRole('button', { name: /tracks, focus areas and settings/i }));

    expect(screen.getByText('Recommended tracks')).toBeTruthy();
    expect(screen.getByText('Focus areas')).toBeTruthy();
  });
});
