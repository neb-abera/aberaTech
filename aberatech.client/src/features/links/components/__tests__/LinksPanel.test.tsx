// @vitest-environment jsdom
/**
 * The bookmark list from two chairs. A visitor gets a sign-in button that
 * brings them back here and nothing is fetched or written beyond the one
 * read that said so. The owner sees the list under its headings, adds a
 * link and it is saved, removes one and that is saved too, and a bad
 * address is refused before anything is sent.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { LinksDocument } from "../../core/links";
import LinksPanel from "../LinksPanel";

const respond = (status: number, body: unknown = null) => ({
  status,
  ok: status >= 200 && status < 300,
  headers: {
    get: (name: string) =>
      name === "content-type" ? "application/json" : null,
  },
  json: async () => body,
});

let fetchMock: ReturnType<typeof vi.fn>;

/** Sign in as the owner with `saved` on the server, or as a visitor. */
function visit(saved: LinksDocument | null | "visitor") {
  fetchMock = vi.fn();
  if (saved === "visitor") fetchMock.mockResolvedValue(respond(401));
  else {
    fetchMock
      .mockResolvedValueOnce(saved ? respond(200, saved) : respond(404))
      .mockResolvedValue(respond(204));
  }
  vi.stubGlobal("fetch", fetchMock);
}

const settle = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
};

const puts = () =>
  fetchMock.mock.calls.filter(([, options]) => options?.method === "PUT");

const lastPut = (): LinksDocument => {
  const calls = puts();
  const [, options] = calls[calls.length - 1];
  return JSON.parse(String(options.body)) as LinksDocument;
};

const saved: LinksDocument = {
  version: 1,
  links: [
    {
      id: "a",
      title: "Tracker",
      url: "https://claude.ai/code/artifact/x",
      group: "Plans",
      note: "",
      addedAt: "2026-09-12",
    },
    {
      id: "b",
      title: "",
      url: "https://www.example.org/handbook",
      group: "",
      note: "the PDF",
      addedAt: "2026-09-12",
    },
  ],
};

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("for a visitor", () => {
  it("offers sign-in that returns here, and writes nothing", async () => {
    visit("visitor");
    render(<LinksPanel />);
    await settle();

    const button = screen.getByRole("link", { name: /sign in with google/i });
    expect(button.getAttribute("href")).toContain("returnUrl=/links");
    expect(screen.queryByRole("form", { name: /add a link/i })).toBeNull();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(puts()).toHaveLength(0);
  });
});

describe("for the owner", () => {
  it("shows the list under its headings, the general list first", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    const headings = screen
      .getAllByRole("heading", { level: 2 })
      .map((h) => h.textContent);
    expect(headings).toEqual(["General", "Plans"]);

    const tracker = screen.getByRole("link", { name: "Tracker" });
    expect(tracker.getAttribute("href")).toBe(
      "https://claude.ai/code/artifact/x",
    );
    expect(tracker.getAttribute("target")).toBe("_blank");
    expect(tracker.getAttribute("rel")).toContain("noopener");

    // An untitled link is named by its host, and its note sits under it.
    const general = screen.getByRole("list", { name: "Links under General" });
    expect(
      within(general).getByRole("link", { name: "example.org" }),
    ).toBeTruthy();
    expect(within(general).getByText(/the PDF/)).toBeTruthy();
    expect(puts()).toHaveLength(0);
  });

  it("adds a link and saves it a beat later", async () => {
    visit(null);
    render(<LinksPanel />);
    await settle();

    expect(screen.getByText(/nothing here yet/i)).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/address/i), {
      target: { value: "abera.tech/fitness" },
    });
    fireEvent.change(screen.getByLabelText(/^title/i), {
      target: { value: "Console" },
    });
    fireEvent.change(screen.getByLabelText(/^group/i), {
      target: { value: "Mine" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /add a link/i }));

    expect(
      screen.getByRole("link", { name: "Console" }).getAttribute("href"),
    ).toBe("https://abera.tech/fitness");
    expect(
      screen.getByRole("heading", { level: 2, name: "Mine" }),
    ).toBeTruthy();
    expect(screen.getByText("Saving…")).toBeTruthy();

    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    await settle();

    expect(puts()).toHaveLength(1);
    const body = lastPut();
    expect(body.links).toHaveLength(1);
    expect(body.links[0]).toMatchObject({
      title: "Console",
      url: "https://abera.tech/fitness",
      group: "Mine",
    });
    expect(screen.getByText("Saved")).toBeTruthy();
  });

  it("refuses an address that is not one, before anything is sent", async () => {
    visit(null);
    render(<LinksPanel />);
    await settle();

    fireEvent.change(screen.getByLabelText(/address/i), {
      target: { value: "javascript:alert(1)" },
    });
    fireEvent.submit(screen.getByRole("form", { name: /add a link/i }));

    expect(screen.getByText(/not a web address/i)).toBeTruthy();
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    expect(puts()).toHaveLength(0);
  });

  it("removes a link and saves the shorter list", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Remove Tracker" }));
    expect(screen.queryByRole("link", { name: "Tracker" })).toBeNull();

    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    await settle();

    expect(puts()).toHaveLength(1);
    expect(lastPut().links.map((l) => l.id)).toEqual(["b"]);
  });

  it("narrows the list to what matches", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    fireEvent.change(screen.getByLabelText(/find/i), {
      target: { value: "handbook" },
    });
    expect(screen.queryByRole("link", { name: "Tracker" })).toBeNull();
    expect(screen.getByRole("link", { name: "example.org" })).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/find/i), {
      target: { value: "zzz" },
    });
    expect(screen.getByText(/nothing matches/i)).toBeTruthy();
    expect(puts()).toHaveLength(0);
  });
});
