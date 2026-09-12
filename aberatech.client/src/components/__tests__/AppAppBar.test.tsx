// @vitest-environment jsdom
/**
 * The phone drawer is the whole navigation on a small screen, so it opening at
 * all is the thing to protect. MUI 9 throws when a MenuItem renders outside a
 * Menu or MenuList, and a throw here unmounts the entire app — the white
 * screen — rather than breaking one control.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router";
import {
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { resetAccountProbeForTests } from "../../hooks/useAccount";
import { guides, label, projects } from "../../site/sections";
import AppTheme from "../../theme/AppTheme";
import AppAppBar from "../AppAppBar";

// Without vitest globals, testing-library cannot register its own cleanup.
afterEach(cleanup);

function mount(entry = "/") {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <AppTheme>
        <AppAppBar />
      </AppTheme>
    </MemoryRouter>,
  );
}

/** The account probe's answer for the next mount. */
function account(signedIn: boolean) {
  resetAccountProbeForTests();
  const fetchMock = vi
    .fn()
    .mockResolvedValue(new Response(JSON.stringify({ signedIn })));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

beforeEach(() => {
  account(false);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

// Same rehearsal as PlannerBoard.mobile: the worker's first MUI mount, first
// role query and first click each pay a one-time emotion/jsdom cost that
// under CI load blows the first test's 5s budget. Paying it here, outside any
// timed test, leaves every test starting from warm caches.
beforeAll(() => {
  mount();
  fireEvent.click(screen.getByRole("button", { name: "Menu button" }));
  cleanup();
}, 30_000);

describe("the phone drawer", () => {
  it("opens without crashing, listing every guide and project", () => {
    mount();

    fireEvent.click(screen.getByRole("button", { name: "Menu button" }));

    for (const entry of [...guides, ...projects]) {
      expect(screen.getByRole("menuitem", { name: label(entry) })).toBeTruthy();
    }
  });

  it("links each entry to its page", () => {
    mount();

    fireEvent.click(screen.getByRole("button", { name: "Menu button" }));

    for (const entry of [...guides, ...projects]) {
      const item = screen.getByRole("menuitem", { name: label(entry) });
      expect(item.getAttribute("href")).toBe(entry.to);
    }
  });
});

describe("the account controls", () => {
  it("offers sign-in to a visitor, returning to the page they were on, and no Links entry", async () => {
    account(false);
    mount("/guides?x=1");

    const signIn = await screen.findByRole("link", { name: "Sign in" });
    expect(signIn.getAttribute("href")).toBe(
      "/api/scheduling/admin/sign-in?returnUrl=%2Fguides%3Fx%3D1",
    );
    expect(screen.queryByRole("link", { name: "Links" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Menu button" }));
    expect(screen.getByRole("menuitem", { name: "Sign in" })).toBeTruthy();
    expect(screen.queryByRole("menuitem", { name: "Links" })).toBeNull();
  });

  it("shows the owner the Links entry and a way out", async () => {
    const fetchMock = account(true);
    mount();

    const links = await screen.findByRole("link", { name: "Links" });
    expect(links.getAttribute("href")).toBe("/links");
    expect(screen.queryByRole("link", { name: "Sign in" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Menu button" }));
    expect(
      screen.getByRole("menuitem", { name: "Links" }).getAttribute("href"),
    ).toBe("/links");

    const reload = vi.fn();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { ...window.location, reload },
    });
    fireEvent.click(screen.getByRole("menuitem", { name: "Sign out" }));

    await waitFor(() => {
      expect(reload).toHaveBeenCalledTimes(1);
    });
    const [url, options] =
      fetchMock.mock.calls[fetchMock.mock.calls.length - 1];
    expect(String(url)).toBe("/api/scheduling/admin/sign-out");
    expect(options?.method).toBe("POST");
  });
});
