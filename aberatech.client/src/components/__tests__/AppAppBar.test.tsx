// @vitest-environment jsdom
/**
 * The phone drawer is the whole navigation on a small screen, so it opening at
 * all is the thing to protect. MUI 9 throws when a MenuItem renders outside a
 * Menu or MenuList, and a throw here unmounts the entire app — the white
 * screen — rather than breaking one control.
 */
import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import AppTheme from '../../theme/AppTheme';
import AppAppBar from '../AppAppBar';
import { guides, label, projects } from '../../site/sections';

// Without vitest globals, testing-library cannot register its own cleanup.
afterEach(cleanup);

function mount() {
  return render(
    <MemoryRouter>
      <AppTheme>
        <AppAppBar />
      </AppTheme>
    </MemoryRouter>
  );
}

describe('the phone drawer', () => {
  it('opens without crashing, listing every guide and project', () => {
    mount();

    fireEvent.click(screen.getByRole('button', { name: 'Menu button' }));

    for (const entry of [...guides, ...projects]) {
      expect(screen.getByRole('menuitem', { name: label(entry) })).toBeTruthy();
    }
  });

  it('links each entry to its page', () => {
    mount();

    fireEvent.click(screen.getByRole('button', { name: 'Menu button' }));

    for (const entry of [...guides, ...projects]) {
      const item = screen.getByRole('menuitem', { name: label(entry) });
      expect(item.getAttribute('href')).toBe(entry.to);
    }
  });
});
