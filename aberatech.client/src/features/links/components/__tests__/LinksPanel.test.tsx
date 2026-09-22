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
  conflicts: [],
  links: [
    {
      id: "a",
      title: "Tracker",
      url: "https://claude.ai/code/artifact/x",
      group: "Plans",
      note: "",
      tags: ["MITRE"],
      addedAt: "2026-09-12",
    },
    {
      id: "b",
      title: "",
      url: "https://www.example.org/handbook",
      group: "",
      note: "the PDF",
      tags: [],
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

  it("edits a link in place and saves the corrected one", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Edit Tracker" }));
    const form = screen.getByRole("form", { name: "Edit Tracker" });
    fireEvent.change(within(form).getByLabelText(/^title/i), {
      target: { value: "Runway" },
    });
    fireEvent.change(within(form).getByLabelText(/^group/i), {
      target: { value: "Plans / 2026" },
    });
    fireEvent.submit(form);

    expect(screen.queryByRole("form", { name: "Edit Tracker" })).toBeNull();
    expect(screen.getByRole("link", { name: "Runway" })).toBeTruthy();
    expect(
      screen.getByRole("heading", { level: 2, name: "Plans / 2026" }),
    ).toBeTruthy();

    await flushSave();
    expect(puts()).toHaveLength(1);
    const body = lastPut();
    expect(body.links.find((l) => l.id === "a")).toMatchObject({
      title: "Runway",
      url: "https://claude.ai/code/artifact/x",
      group: "Plans / 2026",
    });
  });

  it("refuses an edit to a bad address and keeps the form open", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Edit Tracker" }));
    const form = screen.getByRole("form", { name: "Edit Tracker" });
    fireEvent.change(within(form).getByLabelText(/address/i), {
      target: { value: "mailto:x@y" },
    });
    fireEvent.submit(form);

    expect(screen.getByText(/not a web address/i)).toBeTruthy();
    expect(screen.getByRole("form", { name: "Edit Tracker" })).toBeTruthy();
    fireEvent.click(within(form).getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("form", { name: "Edit Tracker" })).toBeNull();
    await flushSave();
    expect(puts()).toHaveLength(0);
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
      screen.getByText(
        /bookmarks\.html: 1 added, 1 updated, 1 already here, 1 to resolve\./,
      ),
    ).toBeTruthy();
    // The tracker kept its name and stayed under Plans, with the file's
    // name waiting to be settled; the untitled handbook got a name; the
    // new page arrived under Reading.
    expect(
      within(screen.getByRole("list", { name: "Links under Plans" })).getByRole(
        "link",
        { name: "Tracker" },
      ),
    ).toBeTruthy();
    const conflict = screen.getByRole("region", {
      name: "Conflict on Tracker",
    });
    expect(within(conflict).getByText("Runway")).toBeTruthy();
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
    expect(body.conflicts).toHaveLength(1);
    expect(body.conflicts[0]).toMatchObject({
      linkId: "a",
      source: "bookmarks.html",
      theirs: { title: "Runway" },
    });

    // Take the file's name: the link changes, the conflict goes, both saved.
    fireEvent.click(
      within(conflict).getByRole("button", { name: "Take the file's" }),
    );
    await settle();
    expect(screen.getByRole("link", { name: "Runway" })).toBeTruthy();
    expect(screen.queryByRole("region", { name: /Conflict on/ })).toBeNull();
    await flushSave();
    expect(lastPut().conflicts).toHaveLength(0);
  });

  it("keeps mine when told to, and edits with the file's values when asked", async () => {
    visit({
      ...saved,
      conflicts: [
        {
          id: "k1",
          linkId: "a",
          source: "old.html",
          seenAt: "2026-09-22",
          theirs: { title: "Runway", note: "from the file" },
        },
        {
          id: "k2",
          linkId: "b",
          source: "old.html",
          seenAt: "2026-09-22",
          theirs: { title: "Handbook" },
        },
      ],
    });
    render(<LinksPanel />);
    await settle();

    const first = screen.getByRole("region", { name: "Conflict on Tracker" });
    fireEvent.click(within(first).getByRole("button", { name: "Keep mine" }));
    await settle();
    expect(screen.getByRole("link", { name: "Tracker" })).toBeTruthy();
    expect(
      screen.queryByRole("region", { name: "Conflict on Tracker" }),
    ).toBeNull();

    const second = screen.getByRole("region", {
      name: "Conflict on example.org",
    });
    fireEvent.click(within(second).getByRole("button", { name: "Edit" }));
    const form = screen.getByRole("form", { name: /edit example\.org/i });
    const title = within(form).getByLabelText("Title") as HTMLInputElement;
    expect(title.value).toBe("Handbook");
    fireEvent.change(title, { target: { value: "Handbook, mine" } });
    fireEvent.submit(form);
    await settle();
    expect(screen.getByRole("link", { name: "Handbook, mine" })).toBeTruthy();
    expect(screen.queryByRole("region", { name: /Conflict on/ })).toBeNull();

    await flushSave();
    expect(lastPut().conflicts).toHaveLength(0);
  });

  it("narrows to a tag, and downloads just those under the tag's name", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    fireEvent.click(
      within(screen.getByRole("group", { name: "Tags" })).getByText("MITRE"),
    );
    expect(screen.getByRole("link", { name: "Tracker" })).toBeTruthy();
    expect(screen.queryByRole("link", { name: "example.org" })).toBeNull();

    const blobs: Blob[] = [];
    const names: string[] = [];
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
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      names.push(this.download);
    });
    fireEvent.click(screen.getByRole("button", { name: "Download" }));
    expect(names[0]).toMatch(/^links-mitre-\d{4}-\d{2}-\d{2}\.html$/);
    const text = await blobs[0].text();
    expect(text).toContain("claude.ai/code/artifact/x");
    expect(text).not.toContain("example.org/handbook");
    expect(text).toContain('TAGS="MITRE"');
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

  it("emails the list as a mailto with the file in the body when there is no share sheet", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    const hrefs: string[] = [];
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(function (this: HTMLAnchorElement) {
        hrefs.push(this.href);
      });

    fireEvent.click(screen.getByRole("button", { name: "Email" }));
    await settle();

    expect(click).toHaveBeenCalledTimes(1);
    expect(hrefs[0].startsWith("mailto:?subject=Links%20")).toBe(true);
    const body = decodeURIComponent(hrefs[0].split("&body=")[1]);
    expect(body).toContain("save it");
    expect(body).toContain("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
  });

  it("hands the file to the share sheet where the browser has one", async () => {
    visit(saved);
    render(<LinksPanel />);
    await settle();

    const share = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal("navigator", {
      ...window.navigator,
      share,
      canShare: () => true,
      clipboard: window.navigator.clipboard,
    });

    fireEvent.click(screen.getByRole("button", { name: "Email" }));
    await settle();

    expect(share).toHaveBeenCalledTimes(1);
    const { files, title } = share.mock.calls[0][0];
    expect(files[0].name).toMatch(/^links-\d{4}-\d{2}-\d{2}\.html$/);
    expect(title.startsWith("Links ")).toBe(true);
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
