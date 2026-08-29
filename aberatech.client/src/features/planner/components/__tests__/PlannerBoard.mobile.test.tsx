// @vitest-environment jsdom
/**
 * On a phone the settings rail is a pane at the top of the board that expands
 * in place, not an overlay. The pane collapsed is the default, so the plan is
 * the first thing seen; expanding it reveals the same rail the wide layout
 * keeps in its left column.
 */
import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
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

  it('sticks to the top of the screen, so it is reachable from the bottom of the page', () => {
    mount();

    const summary = screen.getByRole('button', { name: /tracks, focus areas and settings/i });
    const pane = summary.closest('.MuiAccordion-root') as HTMLElement;
    expect(getComputedStyle(pane).position).toBe('sticky');
  });

  it('pins itself to the screen while open, so expanding near the bottom shows all of it', () => {
    mount();

    const summary = screen.getByRole('button', { name: /tracks, focus areas and settings/i });
    const pane = summary.closest('.MuiAccordion-root') as HTMLElement;

    // Sticky travel ends at the parent's bottom edge, so a pane opened near
    // the bottom of the page would be shoved up and off the screen. While
    // open it is fixed to the viewport instead; closed, it goes back to
    // scrolling with the page.
    fireEvent.click(summary);
    expect(getComputedStyle(pane).position).toBe('fixed');

    fireEvent.click(summary);
    expect(getComputedStyle(pane).position).toBe('sticky');
  });
});

describe('the course card on a narrow screen', () => {
  it('opens as a sheet at the bottom, not a popper beside the chip', () => {
    mount();

    const chip = document.querySelector('[data-code]') as HTMLElement;
    fireEvent.click(chip);

    expect(document.querySelector('.MuiDrawer-root')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Close the course card' })).toBeTruthy();
    expect(document.querySelector('[data-popper-placement]')).toBeNull();
  });

  it('opens on hover, with no backdrop that would steal the pointer and flutter it shut', async () => {
    mount();

    // A modal sheet's backdrop covers the chip the moment it opens, firing
    // mouseleave, closing the sheet, and starting over — so the sheet must be
    // hoverable without being modal.
    fireEvent.mouseOver(document.querySelector('[data-code]') as HTMLElement);

    await waitFor(() => {
      expect(document.querySelector('.MuiDrawer-root')).toBeTruthy();
    });
    expect(document.querySelector('.MuiBackdrop-root')).toBeNull();
    expect(document.querySelector('.MuiModal-root')).toBeNull();
  });

  it('lets the card fill the sheet, rather than capping it at the popper width', () => {
    mount();

    fireEvent.click(document.querySelector('[data-code]') as HTMLElement);

    const paper = document.querySelector('.MuiDrawer-paper') as HTMLElement;
    const card = paper.firstElementChild as HTMLElement;
    expect(getComputedStyle(card).maxWidth).not.toBe('380px');
  });

  it('closes from its own close button', async () => {
    mount();

    fireEvent.click(document.querySelector('[data-code]') as HTMLElement);
    fireEvent.click(screen.getByRole('button', { name: 'Close the course card' }));

    await waitFor(() => {
      expect(screen.queryByRole('button', { name: 'Close the course card' })).toBeNull();
    });
  });
});
