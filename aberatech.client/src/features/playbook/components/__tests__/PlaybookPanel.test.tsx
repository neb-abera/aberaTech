// @vitest-environment jsdom
/**
 * The playbook page from three chairs. A visitor gets a sign-in button that
 * returns here, and no page is asked for. A deployment without Notion says
 * so. The owner sees the tree, the open page drawn block by block, and a
 * download link per file. A page picked in the tree opens without a reload.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import type { PlaybookNode, PlaybookPage } from "../../core/api";
import PlaybookPanel from "../PlaybookPanel";

const root: PlaybookNode = {
  id: "root-id",
  title: "Field notes",
  kind: "page",
  children: [
    { id: "lists-id", title: "Checklists", kind: "page", children: [] },
    {
      id: "db-id",
      title: "Templates",
      kind: "database",
      children: [
        {
          id: "letter-id",
          title: "Letter template",
          kind: "page",
          children: [],
        },
      ],
    },
  ],
};

const rootPage: PlaybookPage = {
  id: "root-id",
  title: "Field notes",
  blocks: [
    { type: "heading", level: 1, text: [{ text: "How this works" }] },
    {
      type: "paragraph",
      text: [
        { text: "Read " },
        { text: "before", bold: true },
        { text: " and " },
        { text: "check", italic: true },
        { text: " " },
        { text: "make check", code: true },
        { text: " see " },
        { text: "the example", href: "https://example.com/" },
        { text: " and " },
        { text: "the list", href: "/playbook?page=lists-id" },
      ],
    },
    {
      type: "numbered",
      text: [{ text: "Gather" }],
      children: [{ type: "bulleted", text: [{ text: "Dates" }] }],
    },
    { type: "numbered", text: [{ text: "Draft" }] },
    { type: "todo", text: [{ text: "Book the room" }], checked: true },
    {
      type: "toggle",
      text: [{ text: "Why it matters" }],
      children: [{ type: "paragraph", text: [{ text: "Inside" }] }],
    },
    { type: "quote", text: [{ text: "Slow is smooth." }] },
    { type: "callout", icon: "!", text: [{ text: "Check twice." }] },
    { type: "code", language: "shell", text: [{ text: "echo ready" }] },
    { type: "divider" },
    {
      type: "table",
      headerRow: true,
      rows: [
        [[{ text: "Item" }], [{ text: "Owner" }]],
        [[{ text: "Agenda" }], [{ text: "Lead" }]],
      ],
    },
    { type: "file", id: "file-id", kind: "pdf", name: "packing-list.pdf" },
    {
      type: "file",
      id: "ext-id",
      kind: "file",
      name: "outside.txt",
      url: "https://example.com/outside.txt",
    },
    { type: "page", id: "lists-id", title: "Checklists" },
    {
      type: "database",
      id: "db-id",
      title: "Templates",
      pages: [
        {
          id: "letter-id",
          title: "Letter template",
          kind: "page",
          children: [],
        },
      ],
    },
    { type: "unsupported", notionType: "made_up" },
  ],
};

let fetchMock: ReturnType<typeof vi.fn>;

const settle = async () => {
  await act(async () => {
    for (let i = 0; i < 6; i++) await Promise.resolve();
  });
};

function mount(
  route: string,
  answer: (url: string) => ReturnType<typeof respond>,
) {
  fetchMock = vi.fn(async (url: string) => answer(String(url)));
  vi.stubGlobal("fetch", fetchMock);
  render(
    <MemoryRouter initialEntries={[route]}>
      <PlaybookPanel />
    </MemoryRouter>,
  );
}

const owner = (url: string) => {
  if (url === "/api/playbook") return respond(200, { configured: true, root });
  if (url === "/api/playbook/pages/root-id") return respond(200, rootPage);
  if (url === "/api/playbook/pages/lists-id")
    return respond(200, {
      id: "lists-id",
      title: "Checklists",
      blocks: [{ type: "paragraph", text: [{ text: "Print these." }] }],
    });
  return respond(404);
};

const asked = () => fetchMock.mock.calls.map(([url]) => String(url));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("a visitor", () => {
  it("is offered sign-in that comes back here, and no page is asked for", async () => {
    mount("/playbook", () => respond(401));
    await settle();

    expect(
      screen
        .getByRole("link", { name: "Sign in with Google" })
        .getAttribute("href"),
    ).toBe("/api/scheduling/admin/sign-in?returnUrl=/playbook");
    expect(asked()).toEqual(["/api/playbook"]);
  });
});

describe("a deployment without Notion", () => {
  it("says Notion is not connected", async () => {
    mount("/playbook", () => respond(200, { configured: false }));
    await settle();

    expect(screen.getByText(/Notion is not connected/)).toBeTruthy();
    expect(asked()).toEqual(["/api/playbook"]);
  });

  it("says Notion is busy on a 503", async () => {
    mount("/playbook", () => respond(503));
    await settle();

    expect(
      screen.getByText("Notion is busy. Reload in a minute."),
    ).toBeTruthy();
  });
});

describe("the owner", () => {
  it("sees the tree and the root page drawn block by block", async () => {
    mount("/playbook", owner);
    await settle();

    const nav = screen.getByRole("navigation", { name: "Playbook pages" });
    expect(
      within(nav)
        .getByRole("link", { name: "Field notes" })
        .getAttribute("aria-current"),
    ).toBe("page");
    expect(within(nav).getByText("Templates").tagName).toBe("SPAN");
    expect(
      within(nav)
        .getByRole("link", { name: "Letter template" })
        .getAttribute("href"),
    ).toBe("/playbook?page=letter-id");

    const article = screen.getByRole("article", { name: "Page" });
    expect(
      within(article).getByRole("heading", { level: 2, name: "Field notes" }),
    ).toBeTruthy();
    expect(
      within(article).getByRole("heading", {
        level: 2,
        name: "How this works",
      }),
    ).toBeTruthy();
    expect(within(article).getByText("before").tagName).toBe("STRONG");
    expect(within(article).getByText("check").tagName).toBe("EM");
    expect(within(article).getByText("make check").tagName).toBe("CODE");
    const external = within(article).getByRole("link", { name: "the example" });
    expect(external.getAttribute("href")).toBe("https://example.com/");
    expect(external.getAttribute("rel")).toBe("noopener noreferrer");
    expect(
      within(article)
        .getByRole("link", { name: "the list" })
        .getAttribute("href"),
    ).toBe("/playbook?page=lists-id");

    const lists = article.querySelectorAll("ol");
    expect(lists).toHaveLength(1);
    expect(lists[0].children).toHaveLength(2);
    expect(within(lists[0]).getByText("Dates").closest("ul")).toBeTruthy();
    expect(
      (
        within(article).getByRole("checkbox", {
          name: "Book the room",
        }) as HTMLInputElement
      ).checked,
    ).toBe(true);
    expect(within(article).getByText("Inside").closest("details")).toBeTruthy();
    expect(
      within(article).getByText("Slow is smooth.").closest("blockquote"),
    ).toBeTruthy();
    expect(within(article).getByRole("note").textContent).toContain(
      "Check twice.",
    );
    expect(within(article).getByText("echo ready").closest("pre")).toBeTruthy();
    expect(
      within(article).getByRole("columnheader", { name: "Owner" }),
    ).toBeTruthy();
    expect(within(article).getByRole("cell", { name: "Lead" })).toBeTruthy();

    const download = within(article).getByRole("link", {
      name: "Download packing-list.pdf",
    });
    expect(download.getAttribute("href")).toBe("/api/playbook/files/file-id");
    expect(download.getAttribute("download")).toBe("packing-list.pdf");
    expect(
      within(article)
        .getByRole("link", { name: "outside.txt" })
        .getAttribute("href"),
    ).toBe("https://example.com/outside.txt");
    expect(within(article).getByText(/A made_up block/)).toBeTruthy();

    expect(asked()).toEqual(["/api/playbook", "/api/playbook/pages/root-id"]);
  });

  it("opens a page picked in the tree", async () => {
    mount("/playbook", owner);
    await settle();

    fireEvent.click(
      within(
        screen.getByRole("navigation", { name: "Playbook pages" }),
      ).getByRole("link", { name: "Checklists" }),
    );
    await settle();

    expect(screen.getByText("Print these.")).toBeTruthy();
    expect(asked()).toContain("/api/playbook/pages/lists-id");
  });

  it("opens the page the address names, and says when it is not in the playbook", async () => {
    mount("/playbook?page=gone-id", owner);
    await settle();

    expect(screen.getByText("That page is not in the playbook.")).toBeTruthy();
  });
});
