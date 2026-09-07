// @vitest-environment jsdom
/**
 * The plan from two chairs. The owner's: every task a checkbox, a tick and
 * a gate attempt saved to the server. A visitor's: the same page read-only,
 * with nothing to tick, nothing to log, and no request that writes.
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
import type { Result } from "../../core/drills";
import { allTasks, plan } from "../../core/plan";
import type { TrainingDocument } from "../../hooks/useTrainingProgress";
import TrainingPlan from "../TrainingPlan";

afterEach(cleanup);

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
function visit(saved: Partial<TrainingDocument> | null | "visitor") {
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

// Same rehearsal as AppAppBar.test: the worker's first MUI mount of this
// page pays a one-time emotion/jsdom cost that under coverage
// instrumentation blew the first test's budget.
beforeAll(async () => {
  visit("visitor");
  render(<TrainingPlan />);
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
  it("shows every block, week range and gate, with nothing to tick", async () => {
    visit("visitor");
    render(<TrainingPlan />);
    await settle();

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
  });

  it("can run the drill, but keeps no history and sends nothing", async () => {
    visit("visitor");
    render(<TrainingPlan />);
    await settle();

    expect(
      screen.getByRole("button", { name: "Start today's drill" }),
    ).toBeTruthy();
    expect(screen.queryByRole("table", { name: "Drill history" })).toBeNull();
    expect(puts()).toHaveLength(0);
  });

  it("is what the build-time render shows too: the first paint is read-only", () => {
    // Before the server has answered, the page must match the prerendered
    // HTML, which was made with no server at all.
    visit("visitor");
    render(<TrainingPlan />);
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });
});

describe("for the owner", () => {
  it("offers every task as a checkbox, ticked from the saved document", async () => {
    visit({
      version: 1,
      done: [allTasks[0].id, allTasks[2].id],
      drills: [],
      gates: {},
    });
    render(<TrainingPlan />);
    await settle();

    const boxes = screen.getAllByRole("checkbox") as HTMLInputElement[];
    expect(boxes).toHaveLength(allTasks.length);
    expect(boxes[0].checked).toBe(true);
    expect(boxes[1].checked).toBe(false);
    expect(boxes[2].checked).toBe(true);
    expect(screen.getByText(`2 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("saves a tick to the server, once, a beat after the click", async () => {
    visit(null);
    render(<TrainingPlan />);
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
    expect(url).toBe("/api/progress/rf-training");
    expect(JSON.parse(options.body).done).toEqual([
      allTasks[0].id,
      allTasks[1].id,
    ]);
  });

  it("can start over", async () => {
    visit({ version: 1, done: [allTasks[0].id], drills: [], gates: {} });
    render(<TrainingPlan />);
    await settle();

    fireEvent.click(screen.getByRole("button", { name: "Start over" }));
    expect(screen.getByText(`0 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("shows the drill history and a gate log for every block", async () => {
    visit({
      version: 1,
      done: [],
      drills: [
        {
          seed: 1,
          at: "2026-09-06T07:00:00.000Z",
          total: 20,
          correct: 17,
          seconds: 512,
          byKind: {} as Result["byKind"],
        },
      ],
      gates: {
        wire: [{ id: "a", on: "2027-01-19", passed: true, minutes: 27 }],
      },
    });
    render(<TrainingPlan />);
    await settle();

    expect(
      screen.getByRole("table", { name: "Drill history" }).textContent,
    ).toContain("17/20");
    for (const block of plan) {
      expect(
        screen.getByRole("region", { name: `Gate for ${block.id}` }),
      ).toBeTruthy();
    }
    expect(
      screen.getByRole("region", { name: "Gate for wire" }).textContent,
    ).toContain("1 of 1 passed. Best 27 min on 2027-01-19.");
  });

  it("saves a gate attempt", async () => {
    visit(null);
    render(<TrainingPlan />);
    await settle();
    const gate = screen.getByRole("region", { name: "Gate for wire" });

    fireEvent.change(within(gate).getByRole("textbox", { name: "Minutes" }), {
      target: { value: "28" },
    });
    fireEvent.click(within(gate).getByRole("button", { name: "Log a pass" }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000);
    });

    expect(gate.textContent).toContain("1 of 1 passed. Best 28 min on");
    const last = puts()[puts().length - 1];
    const saved = JSON.parse(last[1].body);
    expect(saved.gates.wire).toHaveLength(1);
    expect(saved.gates.wire[0]).toMatchObject({ passed: true, minutes: 28 });
  });

  it("opens every reading and practice link on another tab, saying so", async () => {
    visit(null);
    render(<TrainingPlan />);
    await settle();

    for (const block of plan) {
      for (const resource of [...block.resources, ...block.practice]) {
        const links = screen.getAllByRole("link", { name: resource.title });
        for (const link of links) {
          expect(link.getAttribute("target")).toBe("_blank");
          expect(link.getAttribute("rel")).toContain("noopener");
        }
        expect(links.map((link) => link.getAttribute("href"))).toContain(
          resource.url,
        );
      }
    }
  });

  it("carries the plan template and the reference cards", async () => {
    visit(null);
    render(<TrainingPlan />);
    await settle();

    expect(
      screen.getByRole("heading", { name: "Communications plan template" }),
    ).toBeTruthy();
    expect(
      screen.getByRole("heading", { name: "Reference cards" }),
    ).toBeTruthy();
    expect(screen.getByRole("table", { name: "Antenna lengths" })).toBeTruthy();
    expect(screen.getByRole("region", { name: "Net diagram" })).toBeTruthy();
  });
});
