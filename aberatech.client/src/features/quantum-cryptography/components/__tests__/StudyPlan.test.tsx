// @vitest-environment jsdom
/**
 * The plan from two chairs. The owner's: every task a checkbox, a tick and
 * a gate attempt saved to the server under the plan's own key. A visitor's:
 * the same page read-only, with nothing to tick, nothing to log, and no
 * request that writes.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import {
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import type { CurriculumDocument } from "../../../progress/core/curriculum";
import { allTasks, plan, stages, standards } from "../../core/plan";
import StudyPlan from "../StudyPlan";

afterEach(cleanup);

// Sixty-odd checkboxes, twelve gate forms and three tables per render,
// under coverage instrumentation: the same budget the other plan pages
// carry. A real hang still fails, a minute later.
vi.setConfig({ testTimeout: 60_000 });

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
function visit(saved: Partial<CurriculumDocument> | null | "visitor") {
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

// The worker's first MUI mount pays a one-time emotion/jsdom cost that
// under coverage instrumentation blew the first test's budget.
beforeAll(async () => {
  visit("visitor");
  render(<StudyPlan />);
  await settle();
  cleanup();
}, 60_000);

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("for a visitor", () => {
  it("shows every stage, block, week range and gate, with nothing to tick", async () => {
    visit("visitor");
    render(<StudyPlan />);
    await settle();

    for (const stage of stages) {
      expect(screen.getByRole("region", { name: stage.title })).toBeTruthy();
    }
    plan.forEach((block, index) => {
      expect(
        screen.getByRole("heading", { name: `${index + 1}. ${block.title}` }),
      ).toBeTruthy();
      expect(screen.getByText(block.weeks)).toBeTruthy();
      expect(screen.getByText(block.gate)).toBeTruthy();
      expect(
        screen.getByRole("list", { name: `Tasks for ${block.id}` }),
      ).toBeTruthy();
    });
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
    expect(screen.queryByText(/of \d+ done/)).toBeNull();
    expect(screen.queryByRole("button", { name: "Log a pass" })).toBeNull();
    expect(puts()).toHaveLength(0);
  });

  it("is what the build-time render shows too: the first paint is read-only", () => {
    // Before the server has answered, the page must match the prerendered
    // HTML, which was made with no server at all.
    visit("visitor");
    render(<StudyPlan />);
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });

  it("carries the standards table with every document linked", async () => {
    visit("visitor");
    render(<StudyPlan />);
    await settle();

    const table = screen.getByRole("table", { name: "The standards" });
    for (const standard of standards) {
      const link = within(table).getByRole("link", { name: standard.name });
      expect(link.getAttribute("href")).toBe(standard.url);
      expect(link.getAttribute("target")).toBe("_blank");
      expect(link.getAttribute("rel")).toContain("noopener");
      // The row, not the table: three of the FIPS documents share a status.
      const row = link.closest("tr");
      expect(row?.textContent, standard.name).toContain(standard.status);
    }
  });
});

describe("for the owner", () => {
  it("offers every task as a checkbox, ticked from the saved document", async () => {
    visit({
      version: 1,
      done: [allTasks[0].id, allTasks[2].id],
      gates: {},
    });
    render(<StudyPlan />);
    await settle();

    const boxes = screen.getAllByRole("checkbox") as HTMLInputElement[];
    expect(boxes).toHaveLength(allTasks.length);
    expect(boxes[0].checked).toBe(true);
    expect(boxes[1].checked).toBe(false);
    expect(boxes[2].checked).toBe(true);
    expect(screen.getByText(`2 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("saves a tick to the server under the plan's own key, once, a beat after the click", async () => {
    visit(null);
    render(<StudyPlan />);
    await settle();

    fireEvent.click(screen.getByRole("checkbox", { name: allTasks[0].text }));
    fireEvent.click(screen.getByRole("checkbox", { name: allTasks[1].text }));
    expect(screen.getByText(`2 of ${allTasks.length} done`)).toBeTruthy();
    expect(puts()).toHaveLength(0);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    expect(puts()).toHaveLength(1);
    const [url, options] = puts()[0];
    expect(url).toBe("/api/progress/quantum-cryptography");
    expect(JSON.parse(options.body).done).toEqual([
      allTasks[0].id,
      allTasks[1].id,
    ]);
  });

  it("can start over", async () => {
    visit({ version: 1, done: [allTasks[0].id], gates: {} });
    render(<StudyPlan />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Start over" }));
    expect(screen.getByText(`0 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("shows a gate log for every block and saves an attempt", async () => {
    visit({
      version: 1,
      done: [],
      gates: {
        lattices: [{ id: "a", on: "2027-06-01", passed: true, minutes: 90 }],
      },
    });
    render(<StudyPlan />);
    await settle();

    for (const block of plan) {
      expect(
        screen.getByRole("region", { name: `Gate for ${block.id}` }),
      ).toBeTruthy();
    }
    const lattices = screen.getByRole("region", { name: "Gate for lattices" });
    expect(lattices.textContent).toContain(
      "1 of 1 passed. Best 90 min on 2027-06-01.",
    );

    const maths = screen.getByRole("region", { name: "Gate for maths" });
    fireEvent.change(within(maths).getByRole("textbox", { name: "Note" }), {
      target: { value: "600 vectors, all six routines" },
    });
    fireEvent.click(within(maths).getByRole("button", { name: "Log a pass" }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    expect(maths.textContent).toContain("1 of 1 passed.");
    const last = puts()[puts().length - 1];
    const saved = JSON.parse(last[1].body);
    expect(saved.gates.maths).toHaveLength(1);
    expect(saved.gates.maths[0]).toMatchObject({
      passed: true,
      note: "600 vectors, all six routines",
    });
    // The attempt already on the server survives the new one.
    expect(saved.gates.lattices).toHaveLength(1);
  });

  it("opens every reading and practice link on another tab, saying so", async () => {
    visit(null);
    render(<StudyPlan />);
    await settle();

    // One pass over the anchors rather than a role query per resource: the
    // page has a hundred-odd links and a role query walks the whole tree.
    const byHref = new Map<string, HTMLAnchorElement[]>();
    for (const anchor of Array.from(document.querySelectorAll("a[href]"))) {
      const href = anchor.getAttribute("href") ?? "";
      byHref.set(href, [
        ...(byHref.get(href) ?? []),
        anchor as HTMLAnchorElement,
      ]);
    }
    for (const block of plan) {
      for (const resource of [...block.resources, ...block.practice]) {
        const links = byHref.get(resource.url) ?? [];
        expect(links.length, resource.title).toBeGreaterThan(0);
        expect(
          links.map((link) => link.textContent),
          resource.title,
        ).toContain(resource.title);
        for (const link of links) {
          expect(link.getAttribute("target")).toBe("_blank");
          expect(link.getAttribute("rel")).toContain("noopener");
        }
      }
    }
  });

  it("carries the ideas, the cadence and the bookshelf", async () => {
    visit(null);
    render(<StudyPlan />);
    await settle();

    expect(screen.getByRole("table", { name: "The ideas" })).toBeTruthy();
    expect(screen.getByRole("table", { name: "Weekly cadence" })).toBeTruthy();
    expect(screen.getByRole("table", { name: "The bookshelf" })).toBeTruthy();
    expect(screen.getAllByText("Free").length).toBeGreaterThan(2);
  });
});
