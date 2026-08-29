// @vitest-environment jsdom
/**
 * A stored "System" choice must survive page load.
 *
 * This exists because of a real failure. System is offered to signed-in
 * accounts, but the sign-in probe is async — and the mode-correction effect
 * used to run against the probe's initial "no" before the real answer
 * arrived. On every load, an account holder's stored System preference was
 * rewritten to dark; with prerendered pages painting instantly, that was
 * visible as a split-second flash of the (correct!) system scheme before the
 * (wrong) correction stomped it.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, waitFor } from '@testing-library/react';
import AppTheme from '../AppTheme';
import ColorModeIconDropdown from '../ColorModeIconDropdown';
import { resetAccountProbeForTests } from '../../hooks/useAccount';

afterEach(cleanup);

function mount() {
  return render(
    <AppTheme>
      <ColorModeIconDropdown />
    </AppTheme>
  );
}

/**
 * An in-memory Storage, provided explicitly. Node 26 predefines a global
 * localStorage (experimental, undefined without --localstorage-file), and the
 * jsdom test environment defers to that existing global — so neither
 * window.localStorage nor the bare global is usable here. MUI tolerates the
 * gap silently, but these tests assert on what got stored, so they bring
 * their own.
 */
function memoryStorage(): Storage {
  const store = new Map<string, string>();
  return {
    get length() {
      return store.size;
    },
    clear: () => store.clear(),
    getItem: (key: string) => store.get(key) ?? null,
    key: (index: number) => [...store.keys()][index] ?? null,
    removeItem: (key: string) => {
      store.delete(key);
    },
    setItem: (key: string, value: string) => {
      store.set(key, String(value));
    }
  };
}

describe('the stored System preference', () => {
  let storage: Storage;

  beforeEach(() => {
    resetAccountProbeForTests();
    vi.restoreAllMocks();
    storage = memoryStorage();
    vi.stubGlobal('localStorage', storage);
    Object.defineProperty(window, 'localStorage', { value: storage, configurable: true });
    storage.setItem('mui-mode', 'system');
  });

  it('is left alone while the sign-in answer is still pending', async () => {
    // A probe that never answers: the correction must not run on the
    // placeholder "no" while the request is in flight.
    vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => undefined)));

    mount();

    // Give any wrongly-scheduled correction every chance to fire.
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(storage.getItem('mui-mode')).toBe('system');
  });

  it('is kept once the visitor turns out to be signed in', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ signedIn: true }))));

    mount();

    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(storage.getItem('mui-mode')).toBe('system');
  });

  it('is demoted to dark once the visitor turns out to be signed out', async () => {
    // The designed behavior, now waiting for the actual answer: a signed-out
    // visitor has no way to see or change a System setting, so it is
    // corrected — but only after the probe has really said no.
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ signedIn: false }))));

    mount();

    await waitFor(() => {
      expect(storage.getItem('mui-mode')).toBe('dark');
    });
  });
});
