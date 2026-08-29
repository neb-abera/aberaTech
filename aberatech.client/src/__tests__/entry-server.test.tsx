/**
 * The prerender contract: a route rendered at build time is real HTML, not the
 * empty shell it replaces.
 *
 * These run in node on purpose — the build-time renderer has no browser, and
 * anything that reaches for one at render time (rather than in an effect)
 * should fail here rather than in the Docker build.
 */
import { describe, expect, it } from 'vitest';
import { render } from '../entry-server';

describe('build-time rendering', () => {
  it('renders the home page with its content, not a loading fallback', async () => {
    const html = await render('/');

    expect(html).toContain('Neb');
    expect(html).toContain('Abera');
    expect(html).not.toContain('Loading...');
  });

  it('renders a lazy route to completion', async () => {
    // Every view except one is behind React.lazy; prerendering must wait for
    // the chunk rather than snapshotting the Suspense fallback.
    const html = await render('/guides');

    expect(html).toContain('Guides');
    expect(html).not.toContain('Loading...');
  });

  it('inlines the styles the markup needs', async () => {
    // Emotion's zero-config server rendering emits <style data-emotion> next
    // to the components. Without them the first paint is unstyled HTML, which
    // is worse than the blank page this feature replaces.
    const html = await render('/');

    expect(html).toContain('data-emotion');
  });

  it('carries the stored-choice color scheme script', async () => {
    // A visitor who chose light mode would otherwise get a flash of the dark
    // default on every page until hydration. The inline script applies the
    // stored choice before first paint.
    const html = await render('/');

    expect(html).toContain('data-mui-color-scheme');
  });
});
