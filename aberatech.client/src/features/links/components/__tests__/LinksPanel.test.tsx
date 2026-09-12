// @vitest-environment jsdom
/**
 * The bookmark list from two chairs. A visitor gets a sign-in button that
 * brings them back here and nothing is fetched or written beyond the one
 * read that said so. The owner sees the list under its headings, adds a
 * link and it is saved, removes one and that is saved too, a bad address
 * is refused before anything is sent, a refused save says so, a bookmark
 * file folds in without doubling anything, and the list downloads as one.
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
import { respond } from "../../../../test/fakeFetch";
import type { LinksDocument } from "../../core/links";
import LinksPanel from "../LinksPanel";

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

const flushSave = async () => {
  await act(async () => {
    vi.advanceTimersByTime(1000);
  });
  await settle();
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
  vi.restoreAllMocks();
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

    await flushSave();

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
    await flushSave();
    expect(puts()).toHaveLength(0);
  });

  it("removes a link and saves the shorter list", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Remove Tracker" }));
    expect(screen.queryByRole("link", { name: "Tracker" })).toBeNull();

    await flushSave();

    expect(puts()).toHaveLength(1);
    expect(lastPut().links.map((l) => l.id)).toEqual(["b"]);
  });

  it("says when a save was refused and keeps the change on the page", async () => {
    fetchMock = vi
      .fn()
      .mockResolvedValueOnce(respond(200, saved))
      .mockResolvedValue(respond(413));
    vi.stubGlobal("fetch", fetchMock);
    render(<LinksPanel />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Remove Tracker" }));
    await flushSave();

    expect(puts()).toHaveLength(1);
    expect(screen.getByText("Not saved")).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Tracker" })).toBeNull();
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

  it("folds an uploaded bookmark file in without doubling what is here", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    const file = new File(
      [
        `<!DOCTYPE NETSCAPE-Bookmark-file-1>
<DL><p>
    <DT><H3>Bookmarks bar</H3>
    <DL><p>
        <DT><A HREF="https://example.org/handbook">Handbook</A>
        <DT><H3>Reading</H3>
        <DL><p>
            <DT><A HREF="https://claude.ai/code/artifact/x">Runway</A>
            <DT><A HREF="https://new.example/page" ADD_DATE="1725000000">New page</A>
        </DL><p>
    </DL><p>
</DL><p>`,
      ],
      "bookmarks.html",
      { type: "text/html" },
    );
    const input = screen.getByLabelText(/bookmark file to upload/i);
    await act(async () => {
      fireEvent.change(input, { target: { files: [file] } });
      await Promise.resolve();
    });
    await settle();

    expect(
      screen.getByText(/bookmarks\.html: 1 added, 2 updated, 0 already here\./),
    ).toBeTruthy();
    // The tracker took the file's name but stayed under Plans; the
    // untitled handbook got a name; the new page arrived under Reading.
    expect(
      within(screen.getByRole("list", { name: "Links under Plans" })).getByRole(
        "link",
        { name: "Runway" },
      ),
    ).toBeTruthy();
    expect(screen.getByRole("link", { name: "Handbook" })).toBeTruthy();
    expect(
      within(
        screen.getByRole("list", { name: "Links under Reading" }),
      ).getByRole("link", { name: "New page" }),
    ).toBeTruthy();

    await flushSave();
    expect(puts()).toHaveLength(1);
    const body = lastPut();
    expect(body.links).toHaveLength(3);
    expect(body.links.map((l) => l.id).slice(0, 2)).toEqual(["a", "b"]);
  });

  it("refuses a file with no bookmarks in it", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    const file = new File(["just some text"], "notes.txt", {
      type: "text/plain",
    });
    await act(async () => {
      fireEvent.change(screen.getByLabelText(/bookmark file to upload/i), {
        target: { files: [file] },
      });
      await Promise.resolve();
    });
    await settle();

    expect(screen.getByText(/no bookmarks found in notes\.txt/i)).toBeTruthy();
    await flushSave();
    expect(puts()).toHaveLength(0);
  });

  it("downloads the list as a bookmark file", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    const blobs: Blob[] = [];
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn((blob: Blob) => {
          blobs.push(blob);
          return "blob:links";
        }),
        revokeObjectURL: vi.fn(),
      }),
    );
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(() => {});

    fireEvent.click(screen.getByRole("button", { name: "Download" }));

    expect(click).toHaveBeenCalledTimes(1);
    expect(blobs).toHaveLength(1);
    const text = await blobs[0].text();
    expect(text.startsWith("<!DOCTYPE NETSCAPE-Bookmark-file-1>")).toBe(true);
    expect(text).toContain('<A HREF="https://claude.ai/code/artifact/x"');
    expect(text).toContain("<DT><H3>Plans</H3>");
    expect(puts()).toHaveLength(0);
  });
});
