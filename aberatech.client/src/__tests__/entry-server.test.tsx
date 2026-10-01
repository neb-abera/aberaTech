/**
 * The prerender contract: a route rendered at build time is real HTML, not the
 * empty shell it replaces.
 *
 * These run in node on purpose — the build-time renderer has no browser, and
 * anything that reaches for one at render time (rather than in an effect)
 * should fail here rather than in the Docker build.
 */
import { Suspense } from "react";
import { describe, expect, it } from "vitest";
import { render, renderTree } from "../entry-server";
import { heroAvatar } from "../site/meta";
import { checkPage } from "../site/prerenderCheck";
import { prerenderedRoutes } from "../site/prerenderedRoutes";

function Broken(): never {
  throw new TypeError(
    "Cannot read properties of undefined (reading 'replace')",
  );
}

describe("build-time rendering", () => {
  it("renders the home page with its content, not a loading fallback", async () => {
    const html = await render("/");

    expect(html).toContain("Neb");
    expect(html).toContain("Abera");
    expect(html).not.toContain("Loading...");
  });

  it("renders a lazy route to completion", async () => {
    // Every view except one is behind React.lazy; prerendering must wait for
    // the chunk rather than snapshotting the Suspense fallback.
    const html = await render("/guides");

    expect(html).toContain("Guides");
    expect(html).not.toContain("Loading...");
  });

  it("renders the frame of /links around its spinner, and no list", async () => {
    // The owner's list is never in the build: the page is prerendered for
    // its title and intro, and the list arrives from the API after load.
    const html = await render("/links");

    expect(html).toContain("One list, kept on the server");
    expect(html).toContain('aria-label="Loading"');
    expect(html).not.toContain("Sign in with Google");
    expect(html).not.toContain("Loading...");
  });

  for (const [path, title] of [
    ["/schedule", "Schedule time with me"],
    ["/schedule/admin", "Run the queue"],
    ["/fitness", "Military athlete console"],
    ["/plan", "Plan"],
    ["/devbox", "Dev box"],
    ["/alerts", "Alerts"],
    ["/dates", "Dates and countdowns"],
  ]) {
    it(`renders the frame of ${path} around its spinner, and nothing of the owner's`, async () => {
      const html = await render(path);

      expect(html).toContain(`>${title}</h1>`);
      expect(html).toMatch(/aria-label="Loading/);
      expect(html).not.toContain("Sign in with Google");
      expect(html).not.toContain("Loading...");
    });
  }

  it("renders the planner board a first visitor sees", async () => {
    // The board is built from the catalog in the bundle, so the build can
    // draw all of it. What only the browser knows (who is asking, the
    // colour scheme, the width of the screen) must not change the markup,
    // or hydration moves the page: the save line says it is checking, and
    // the colours are CSS variables carrying both schemes.
    const html = await render("/planner");

    expect(html).toContain(">Graduate course planner</h1>");
    expect(html).toContain("Degree rules");
    expect(html).toContain("The plan");
    expect(html).toContain("Recommended tracks");
    expect(html).toContain("data-code=");
    expect(html).toContain("Checking for a saved plan");
    expect(html).toContain("var(--planner-area-");
    expect(html).not.toContain("Loading...");
  });

  it("renders the training plan without a browser", async () => {
    // The plan loads its ticks from the server in an effect. Read at render
    // time that would throw here and mismatch on hydration; read in an
    // effect it is invisible to the build, and the page comes out with
    // every box empty.
    const html = await render("/rf-training");

    expect(html).toContain("Tactically Relevant RF Training");
    expect(html).toContain("Licence and the arithmetic");
    expect(html).not.toContain("Loading...");
  });

  it("renders the study plan without a browser", async () => {
    // Same contract as the training plan: the owner's ticks are loaded in
    // an effect, so the build sees the page with every box empty and the
    // stages and blocks all present.
    const html = await render("/signal-processing");

    expect(html).toContain("Learning Signal Processing");
    expect(html).toContain("Beginner");
    expect(html).not.toContain("Loading...");
  });

  it("renders the cryptography plan without a browser", async () => {
    // Same contract as the training plan: the owner's ticks are loaded in
    // an effect, so the build sees the page with every box empty and the
    // stages, blocks and standards table all present.
    const html = await render("/quantum-cryptography");

    expect(html).toContain("Learning Quantum and Post-Quantum Cryptography");
    expect(html).toContain("Classical cryptography, properly");
    expect(html).toContain("FIPS 203, ML-KEM");
    expect(html).not.toContain("Loading...");
  });

  it("draws the avatar the head preloads, sized so nothing shifts when it lands", async () => {
    const html = await render("/");
    const img = html.match(/<img[^>]*alt="Neb Abera"[^>]*>/)?.[0] ?? "";

    expect(img).toContain(heroAvatar.src);
    expect(img).toContain(`srcSet="${heroAvatar.srcSet}"`);
    expect(img).toContain(`sizes="${heroAvatar.sizes}"`);
    expect(img).toContain('width="336"');
    expect(img).toContain('height="336"');
    expect(img).toMatch(/fetchpriority="high"/i);
    expect(html).not.toContain("/headshot.jpg");
  });

  it("inlines the styles the markup needs", async () => {
    // Emotion's zero-config server rendering emits <style data-emotion> next
    // to the components. Without them the first paint is unstyled HTML, which
    // is worse than the blank page this feature replaces.
    const html = await render("/");

    expect(html).toContain("data-emotion");
  });

  it("carries the stored-choice color scheme script", async () => {
    // A visitor who chose light mode would otherwise get a flash of the dark
    // default on every page until hydration. The inline script applies the
    // stored choice before first paint.
    const html = await render("/");

    expect(html).toContain("data-mui-color-scheme");
  });

  it("fails a render whose component throws inside a boundary, naming the route", async () => {
    // React writes such a boundary for the browser to render again and
    // renderToString reports nothing. On 2026-09-28 that shipped every page
    // with an empty root and a green build.
    await expect(
      renderTree("/broken", () => (
        <main>
          <h1>Title</h1>
          <Suspense fallback={<p>Loading...</p>}>
            <Broken />
          </Suspense>
        </main>
      )),
    ).rejects.toThrow(
      /prerender \/broken: React reported 1 error while rendering: Cannot read properties of undefined/,
    );
  });

  it("fails a render whose component throws outside every boundary", async () => {
    await expect(renderTree("/broken", () => <Broken />)).rejects.toThrow(
      /prerender \/broken: React reported 1 error/,
    );
  });

  for (const route of prerenderedRoutes) {
    it(`renders ${route} with text and an h1, as the build requires`, async () => {
      const html = await render(route);

      expect(() => checkPage(route, html)).not.toThrow();
    });
  }
});
