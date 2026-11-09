// @vitest-environment jsdom
/**
 * /training from two chairs. A visitor reads the syllabus and is never
 * sent the owner's document. The owner ticks an item, starts a
 * checkpoint, and sees each saved.
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
import { resetAccountProbeForTests } from "../../../../hooks/useAccount";
import { respond } from "../../../../test/fakeFetch";
import TrainingPanel from "../TrainingPanel";

let fetchMock: ReturnType<typeof vi.fn>;

const judgesUp = {
  status: 200,
  body: {
    configured: true,
    codeforces: {
      handle: "Nebikinizer",
      ok: true,
      rating: null,
      ratedContests: 0,
      solved: { "2263B": "2026-11-10T09:40:00Z" },
      readAt: "2026-11-10T10:00:00Z",
    },
    atcoder: null,
    contests: [],
  },
};
let judges: { status: number; body: unknown } = judgesUp;
let drillRun: { status: number; body: unknown } = { status: 404, body: null };

function visit(saved: unknown | "visitor") {
  fetchMock = vi.fn((url: string, options?: RequestInit) => {
    const path = String(url);
    if (path === "/api/scheduling/admin/me")
      return Promise.resolve(respond(200, { signedIn: saved !== "visitor" }));
    if (path === "/api/progress/training")
      return Promise.resolve(
        options?.method === "PUT"
          ? respond(204)
          : saved
            ? respond(200, saved)
            : respond(404),
      );
    if (path === "/api/training/drills/run")
      return Promise.resolve(respond(drillRun.status, drillRun.body));
    if (path.startsWith("/api/training/solves"))
      return Promise.resolve(respond(judges.status, judges.body));
    return Promise.resolve(respond(404));
  });
  vi.stubGlobal("fetch", fetchMock);
}

const settle = async () => {
  await act(async () => {
    for (let i = 0; i < 6; i++) await Promise.resolve();
  });
};

const show = (route: string) =>
  render(
    <MemoryRouter initialEntries={[route]}>
      <TrainingPanel />
    </MemoryRouter>,
  );

const puts = () =>
  fetchMock.mock.calls.filter(([, options]) => options?.method === "PUT");

beforeEach(() => {
  judges = judgesUp;
  resetAccountProbeForTests();
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("a visitor", () => {
  it("reads the whole map, sees no progress, and never asks for the document", async () => {
    visit("visitor");
    show("/training");
    await screen.findByRole("link", { name: "Sign in with Google" });
    expect(screen.getByRole("region", { name: /Phase 0/ })).toBeTruthy();
    expect(
      screen.getByRole("link", { name: "C++ for contests: the language" }),
    ).toBeTruthy();
    expect(screen.queryByLabelText("Overall progress")).toBeNull();
    expect(fetchMock.mock.calls.map(([url]) => String(url))).toEqual([
      "/api/scheduling/admin/me",
    ]);
  });

  it("reads a lesson with its pointers and no controls", async () => {
    visit("visitor");
    show("/training?lesson=cp-3");
    const signIn = await screen.findByRole("link", {
      name: "Sign in with Google",
    });
    expect(signIn.getAttribute("href")).toBe(
      "/api/scheduling/admin/sign-in?returnUrl=%2Ftraining%3Flesson%3Dcp-3",
    );
    const read = screen.getByRole("article", {
      name: /Introduction to Counting & Probability, chapter 3/,
    });
    expect(within(read).getByText(/pp\. 49 to 62/)).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
  });
});

describe("the owner", () => {
  it("opens on Today, with the plan's block and its first work", async () => {
    visit(null);
    show("/training");
    const today = await screen.findByRole("region", { name: "Today" });
    expect(within(today).getByText(/Phase \d · week \d+ of \d+/)).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Today" })).toBeTruthy();
  });

  it("ticks an item and it is saved", async () => {
    visit(null);
    show("/training?lesson=cp-3");
    await settle();
    const box = await screen.findByRole("checkbox", {
      name: /Done: Introduction to Counting & Probability, chapter 3/,
    });
    fireEvent.click(box);
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    await settle();
    expect(puts()).toHaveLength(1);
    const body = JSON.parse(String(puts()[0][1]?.body));
    expect(Object.keys(body.items)).toEqual(["cp-3-read"]);
  });

  it("starts a checkpoint and sees its clock", async () => {
    visit(null);
    show("/training?unit=bronze-1");
    await settle();
    fireEvent.click(
      await screen.findByRole("button", { name: "Start the checkpoint" }),
    );
    expect(await screen.findByText(/Time is up at/)).toBeTruthy();
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    await settle();
    const body = JSON.parse(String(puts()[0][1]?.body));
    expect(body.checkpoints["bronze-1"]).toHaveLength(1);
  });

  it("sees a placed-out unit marked with its date, its lessons still open", async () => {
    visit({
      checkpoints: { "cpp-basics": [{ started: "2026-11-10T10:00:00.000Z" }] },
      items: {
        "cses-1068": { at: "2026-11-10T10:05:00.000Z" },
        "cses-1083": { at: "2026-11-10T10:10:00.000Z" },
        "cses-1069": { at: "2026-11-10T10:20:00.000Z" },
      },
    });
    show("/training?view=map");
    await settle();
    const unit = await screen.findByRole("listitem", {
      name: "C++ for contests: the language",
    });
    expect(await within(unit).findByText(/^Placed out /)).toBeTruthy();
    expect(
      within(unit).getByRole("link", {
        name: "C++ for contests: the language",
      }),
    ).toBeTruthy();
  });

  it("sees a Codeforces solve from the API, and an unsolved problem with no tick", async () => {
    visit(null);
    show("/training?lesson=band-900-1");
    const solved = await screen.findByRole("article", {
      name: /Codeforces 2263B/,
    });
    expect(await within(solved).findByText(/\(Codeforces API\)/)).toBeTruthy();
    expect(within(solved).queryByRole("checkbox")).toBeNull();
    const open = screen.getByRole("article", { name: /Codeforces 2254B/ });
    expect(
      within(open).getByText(/counts when the Codeforces API sees it solved/),
    ).toBeTruthy();
    expect(within(open).queryByRole("checkbox")).toBeNull();
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).includes("/api/training/solves?codeforces=Nebikinizer"),
      ),
    ).toBe(true);
  });

  it("ticks a Codeforces problem by hand when the judges cannot be read", async () => {
    judges = { status: 502, body: null };
    visit(null);
    show("/training?lesson=band-900-1");
    expect(
      await screen.findByText(/The judges could not be read/),
    ).toBeTruthy();
    const open = screen.getByRole("article", { name: /Codeforces 2254B/ });
    expect(within(open).getByRole("checkbox")).toBeTruthy();
  });

  it("writes a note on a lesson and it is saved with its time", async () => {
    visit(null);
    show("/training?lesson=cp-3");
    const note = await screen.findByRole("textbox", { name: "Lesson notes" });
    fireEvent.change(note, { target: { value: "Divide by the symmetries." } });
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    await settle();
    const body = JSON.parse(String(puts()[puts().length - 1]?.[1]?.body));
    expect(body.notes["cp-3"].text).toBe("Divide by the symmetries.");
    expect(await screen.findByText(/^Kept /)).toBeTruthy();
  });

  it("runs a drill, sees each test, and passing every one marks it done", async () => {
    drillRun = {
      status: 200,
      body: {
        compiled: true,
        tests: [
          { passed: true, actual: "5" },
          { passed: false, actual: "1", error: null },
          { passed: true, actual: "999999999999999999" },
        ],
      },
    };
    visit(null);
    show("/training?lesson=cpp-drills");
    const drill = await screen.findByRole("region", {
      name: "Drill: Read two numbers, print their sum",
    });
    const editor = within(drill).getByRole("textbox", { name: "Your C++" });
    fireEvent.change(editor, { target: { value: "int main() {}" } });
    fireEvent.click(within(drill).getByRole("button", { name: "Run 3 tests" }));
    expect(await within(drill).findByText("2 of 3 tests passed.")).toBeTruthy();
    expect(within(drill).getByText("Test 2: failed")).toBeTruthy();
    const run = fetchMock.mock.calls.find(
      ([url]) => String(url) === "/api/training/drills/run",
    );
    const sent = JSON.parse(String(run?.[1]?.body));
    expect(sent.source).toBe("int main() {}");
    expect(sent.tests).toHaveLength(3);

    drillRun = {
      status: 200,
      body: {
        compiled: true,
        tests: [
          { passed: true, actual: "5" },
          { passed: true, actual: "0" },
          { passed: true, actual: "999999999999999999" },
        ],
      },
    };
    fireEvent.click(within(drill).getByRole("button", { name: "Run 3 tests" }));
    expect(await within(drill).findByText("3 of 3 tests passed.")).toBeTruthy();
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });
    await settle();
    const body = JSON.parse(String(puts()[puts().length - 1]?.[1]?.body));
    expect(body.items["drill-sum"]).toBeTruthy();
    expect(body.code["drill-sum"]).toBe("int main() {}");
  });
});
