// @vitest-environment jsdom
/**
 * The plan from two chairs: a visitor gets a sign-in button that returns
 * here and nothing is written; the owner sees the document rendered, can
 * edit it and the edit is saved a beat later, can upload a file which
 * replaces it, and can download it.
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
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import PlanPanel, { type PlanDocument } from "../PlanPanel";

let fetchMock: ReturnType<typeof vi.fn>;

function visit(saved: PlanDocument | null | "visitor") {
  fetchMock = vi.fn();
  if (saved === "visitor") fetchMock.mockResolvedValue(respond(401));
  else {
    fetchMock
      .mockResolvedValueOnce(saved ? respond(200, saved) : respond(404))
      .mockResolvedValue(respond(204));
  }
  vi.stubGlobal("fetch", fetchMock);
}

const draw = () =>
  render(
    <MemoryRouter>
      <PlanPanel />
    </MemoryRouter>,
  );

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

const saved: PlanDocument = {
  version: 1,
  markdown:
    "# Runway\n\n## Phase 0\n\n- Join the **civic** association\n- Publish",
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
    draw();
    await settle();

    const button = screen.getByRole("link", { name: /sign in with google/i });
    expect(button.getAttribute("href")).toContain("returnUrl=/plan");
    expect(screen.queryByRole("button", { name: "Edit" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Network" })).toBeNull();
    // A visitor is never shown the plan, so the plan is never asked for.
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/progress/plan",
    ]);
    expect(puts()).toHaveLength(0);
  });
});

describe("for the owner", () => {
  it("links Network, asks for the plan page, and puts the runway under it", async () => {
    visit(saved);
    draw();
    await settle();

    expect(
      screen.getByRole("link", { name: "Network" }).getAttribute("href"),
    ).toBe("/network");
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/progress/plan",
      "/api/career-plan",
    ]);
    const runway = screen.getByRole("region", { name: "Edited on this site" });
    expect(
      within(runway).getByRole("heading", { name: "Runway" }),
    ).toBeTruthy();
    expect(within(runway).getByRole("button", { name: "Edit" })).toBeTruthy();
  });

  it("renders the document", async () => {
    visit(saved);
    draw();
    await settle();

    expect(screen.getByRole("heading", { name: "Runway" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Phase 0" })).toBeTruthy();
    const items = screen.getAllByRole("listitem").map((li) => li.textContent);
    expect(items).toEqual(["Join the civic association", "Publish"]);
    expect(puts()).toHaveLength(0);
  });

  it("edits in place and saves a beat later", async () => {
    visit(null);
    draw();
    await settle();

    expect(screen.getByText(/nothing here yet/i)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Edit" }));
    fireEvent.change(screen.getByLabelText(/plan, in markdown/i), {
      target: { value: "# New\n\ntext" },
    });
    expect(screen.getByText("Saving…")).toBeTruthy();

    await flushSave();
    expect(puts()).toHaveLength(1);
    const [, options] = puts()[0];
    expect(JSON.parse(String(options.body))).toEqual({
      version: 1,
      markdown: "# New\n\ntext",
    });

    fireEvent.click(screen.getByRole("button", { name: "Done" }));
    expect(screen.getByRole("heading", { name: "New" })).toBeTruthy();
    expect(screen.getByText("Saved")).toBeTruthy();
  });

  it("replaces the document with an uploaded file", async () => {
    visit(saved);
    draw();
    await settle();

    const file = new File(["# Uploaded\n\n- one"], "plan.md", {
      type: "text/markdown",
    });
    await act(async () => {
      fireEvent.change(screen.getByLabelText(/markdown file to upload/i), {
        target: { files: [file] },
      });
      await Promise.resolve();
    });
    await settle();

    expect(screen.getByRole("heading", { name: "Uploaded" })).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Runway" })).toBeNull();

    await flushSave();
    expect(puts()).toHaveLength(1);
  });

  it("downloads the document as a Markdown file", async () => {
    visit(saved);
    draw();
    await settle();

    const blobs: Blob[] = [];
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn((blob: Blob) => {
          blobs.push(blob);
          return "blob:plan";
        }),
        revokeObjectURL: vi.fn(),
      }),
    );
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(() => {});

    fireEvent.click(screen.getByRole("button", { name: "Download" }));

    expect(click).toHaveBeenCalledTimes(1);
    expect(await blobs[0].text()).toBe(saved.markdown);
    expect(puts()).toHaveLength(0);
  });
});
