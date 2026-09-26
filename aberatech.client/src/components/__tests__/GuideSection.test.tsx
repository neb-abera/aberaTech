// @vitest-environment jsdom
/**
 * A guide section as the browser meets it: prerendered HTML first, React
 * later. A reader who opens a section in between has opened the native
 * `<details>`, and React must keep it open when it hydrates. How the click
 * lands in a real browser with the script held back is
 * e2e/guides.spec.ts.
 */

import { act, cleanup } from "@testing-library/react";
import { useContext } from "react";
import { hydrateRoot, type Root } from "react-dom/client";
import { renderToString } from "react-dom/server";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it } from "vitest";
import GuideSection, { SectionOpen } from "../GuideSection";

function Probe() {
  return (
    <p data-testid="probe">{useContext(SectionOpen) ? "open" : "closed"}</p>
  );
}

const page = (
  <MemoryRouter>
    <GuideSection id="leave" title="Terminal leave">
      <Probe />
    </GuideSection>
  </MemoryRouter>
);

let root: Root | undefined;
afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
  document.body.innerHTML = "";
  cleanup();
});

/** The prerendered page, with `before` run on it ahead of hydration. */
async function hydrate(before: (details: HTMLDetailsElement) => void) {
  const container = document.createElement("div");
  container.innerHTML = renderToString(page);
  document.body.append(container);
  const details = container.querySelector("details");
  expect(details, "the section is not a <details>").not.toBeNull();
  before(details as HTMLDetailsElement);
  await act(async () => {
    root = hydrateRoot(container, page);
  });
  return {
    details: details as HTMLDetailsElement,
    probe: () => container.querySelector('[data-testid="probe"]')?.textContent,
  };
}

describe("GuideSection", () => {
  it("prerenders closed, with the section's words in the HTML", () => {
    const html = renderToString(page);

    expect(html).toMatch(/<details\b[^>]*\bid="leave"/);
    expect(html).not.toMatch(/<details\b[^>]*\bopen/);
    expect(html).toContain("<summary");
    expect(html).toContain("Terminal leave");
    expect(html).toContain("closed");
    expect(html).not.toContain("data-hydrated");
  });

  it("stays open when the reader opened it before hydration", async () => {
    const { details, probe } = await hydrate((details) => {
      details.open = true;
    });

    expect(details.open).toBe(true);
    expect(probe()).toBe("open");
  });

  it("stays closed when nobody opened it", async () => {
    const { details, probe } = await hydrate(() => {});

    expect(details.open).toBe(false);
    expect(probe()).toBe("closed");
    expect(details.hasAttribute("data-hydrated")).toBe(true);
  });

  it("follows the browser when the reader opens and closes it", async () => {
    const { details, probe } = await hydrate(() => {});

    await act(async () => {
      details.open = true;
      details.dispatchEvent(new Event("toggle"));
    });
    expect(probe()).toBe("open");

    await act(async () => {
      details.open = false;
      details.dispatchEvent(new Event("toggle"));
    });
    expect(probe()).toBe("closed");
  });
});
