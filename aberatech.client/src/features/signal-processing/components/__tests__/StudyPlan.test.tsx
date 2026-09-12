// @vitest-environment jsdom
/**
 * The plan from two chairs. The owner's: every task a checkbox, a tick and
 * a gate attempt saved to the server under this plan's own key. A
 * visitor's: the same page read-only, with nothing to tick, nothing to
 * log, and no request that writes.
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
import { allTasks, bookshelf, plan, stages } from "../../core/plan";
import StudyPlan from "../StudyPlan";

afterEach(cleanup);

// The same calibration as the radio plan's page: a heavy MUI mount under
// coverage instrumentation, where a timed-out test leaves its fake timers
// and fetch stub behind for every test after it.
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
  it("shows every stage, block and gate, with nothing to tick", async () => {
    visit("visitor");
    render(<StudyPlan />);
    await settle();

    for (const stage of stages) {
      expect(screen.getByRole("heading", { name: stage.title })).toBeTruthy();
    }
    plan.forEach((block, index) => {
      expect(
        screen.getByRole("heading", {
          level: 3,
          name: `${index + 1}. ${block.title}`,
        }),
      ).toBeTruthy();
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
    visit("visitor");
    render(<StudyPlan />);
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });

  it("carries the ideas and the bookshelf", async () => {
    visit("visitor");
    render(<StudyPlan />);
    await settle();

    expect(screen.getByRole("table", { name: "The ideas" })).toBeTruthy();
    const shelf = screen.getByRole("table", { name: "The bookshelf" });
    for (const book of bookshelf) {
      const link = within(shelf).getByRole("link", { name: book.title });
      expect(link.getAttribute("href")).toBe(book.url);
      expect(link.getAttribute("target")).toBe("_blank");
      expect(link.getAttribute("rel")).toContain("noopener");
    }
  });
});

describe("for the owner", () => {
  it("offers every task as a checkbox, ticked from the saved document", async () => {
    visit({ version: 1, done: [allTasks[0].id, allTasks[2].id], gates: {} });
    render(<StudyPlan />);
    await settle();

    const boxes = screen.getAllByRole("checkbox") as HTMLInputElement[];
    expect(boxes).toHaveLength(allTasks.length);
    expect(boxes[0].checked).toBe(true);
    expect(boxes[1].checked).toBe(false);
    expect(boxes[2].checked).toBe(true);
    expect(screen.getByText(`2 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("saves a tick under this plan's own key, once, a beat after the click", async () => {
    visit(null);
    render(<StudyPlan />);
    await settle();

    fireEvent.click(screen.getByRole("checkbox", { name: allTasks[0].text }));
    fireEvent.click(screen.getByRole("checkbox", { name: allTasks[1].text }));
    expect(puts()).toHaveLength(0);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    expect(puts()).toHaveLength(1);
    const [url, options] = puts()[0];
    expect(url).toBe("/api/progress/signal-processing");
    expect(JSON.parse(options.body)).toEqual({
      version: 1,
      done: [allTasks[0].id, allTasks[1].id],
      gates: {},
    });
  });

  it("can start over", async () => {
    visit({ version: 1, done: [allTasks[0].id], gates: {} });
    render(<StudyPlan />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Start over" }));
    expect(screen.getByText(`0 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("shows a gate log for every block and saves an attempt", async () => {
    const first = plan[0].id;
    visit({
      version: 1,
      done: [],
      gates: {
        [first]: [{ id: "a", on: "2027-01-19", passed: true, minutes: 40 }],
      },
    });
    render(<StudyPlan />);
    await settle();

    for (const block of plan) {
      expect(
        screen.getByRole("region", { name: `Gate for ${block.id}` }),
      ).toBeTruthy();
    }
    const gate = screen.getByRole("region", { name: `Gate for ${first}` });
    expect(gate.textContent).toContain("1 of 1 passed. Best 40 min on");

    fireEvent.change(within(gate).getByRole("textbox", { name: "Note" }), {
      target: { value: "problem set 9 of 10" },
    });
    fireEvent.click(within(gate).getByRole("button", { name: "Log a fail" }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    const saved = JSON.parse(puts()[puts().length - 1][1].body);
    expect(saved.gates[first]).toHaveLength(2);
    expect(saved.gates[first][1]).toMatchObject({
      passed: false,
      note: "problem set 9 of 10",
    });
  });

  it("opens every reading and practice link on another tab, saying so", async () => {
    visit(null);
    render(<StudyPlan />);
    await settle();

    // One query for every link, then a lookup per resource: a query per
    // resource walks this page's whole tree a hundred times and has timed
    // out under coverage on a loaded machine.
    const byHref = new Map<string, HTMLElement[]>();
    for (const link of screen.getAllByRole("link")) {
      const href = link.getAttribute("href") ?? "";
      byHref.set(href, [...(byHref.get(href) ?? []), link]);
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
});
