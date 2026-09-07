// @vitest-environment jsdom
/**
 * The drill from the visitor's chair: start it, answer it, get scored,
 * come back tomorrow and see yesterday in the table.
 */

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { makeSession } from "../../core/drills";
import { storageKey } from "../../hooks/useDrillHistory";
import DrillPanel from "../DrillPanel";

afterEach(cleanup);

function memoryStorage(): Storage {
  const store = new Map<string, string>();
  return {
    get length() {
      return store.size;
    },
    clear: () => store.clear(),
    getItem: (key: string) => store.get(key) ?? null,
    key: (index: number) => [...store.keys()][index] ?? null,
    removeItem: (key: string) => {
      store.delete(key);
    },
    setItem: (key: string, value: string) => {
      store.set(key, String(value));
    },
  };
}

let storage: Storage;

beforeEach(() => {
  storage = memoryStorage();
  vi.stubGlobal("localStorage", storage);
  Object.defineProperty(window, "localStorage", {
    value: storage,
    configurable: true,
  });
});

/** A clock that advances thirty seconds per reading. */
function ticking(start = Date.UTC(2026, 8, 7, 12, 0, 0)) {
  let t = start;
  return () => {
    const now = new Date(t);
    t += 30_000;
    return now;
  };
}

const answerBox = () => screen.getByRole("textbox", { name: "Answer" });

function answer(text: string) {
  fireEvent.change(answerBox(), { target: { value: text } });
  fireEvent.click(screen.getByRole("button", { name: "Check" }));
}

describe("the daily drill", () => {
  it("starts on demand and shows the first problem with its kind", () => {
    const session = makeSession(11, 5);
    render(<DrillPanel seed={11} count={5} now={ticking()} />);

    fireEvent.click(
      screen.getByRole("button", { name: "Start today's drill" }),
    );

    expect(screen.getByText(session.problems[0].prompt)).toBeTruthy();
    expect(screen.getByText("1 of 5")).toBeTruthy();
  });

  it("marks a right answer right, a wrong one wrong, and shows the working", () => {
    const session = makeSession(11, 5);
    render(<DrillPanel seed={11} count={5} now={ticking()} />);
    fireEvent.click(
      screen.getByRole("button", { name: "Start today's drill" }),
    );

    answer(session.problems[0].answer);
    expect(screen.getByRole("status").textContent).toContain("Right.");
    expect(screen.getByText(session.problems[0].explanation)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    answer("definitely not");
    expect(screen.getByRole("status").textContent).toContain(
      `Wrong. The answer is ${session.problems[1].answer}`,
    );
  });

  it("scores the session, times it, and keeps it in the history", () => {
    const session = makeSession(11, 5);
    render(<DrillPanel seed={11} count={5} now={ticking()} />);
    fireEvent.click(
      screen.getByRole("button", { name: "Start today's drill" }),
    );

    session.problems.forEach((problem, i) => {
      answer(i < 3 ? problem.answer : "wrong");
      fireEvent.click(
        screen.getByRole("button", { name: i === 4 ? "Finish" : "Next" }),
      );
    });

    // The clock is read once to start and once to finish: 30 seconds apart.
    expect(screen.getByText("3 of 5 in 0:30")).toBeTruthy();
    const stored = JSON.parse(storage.getItem(storageKey) ?? "[]");
    expect(stored).toHaveLength(1);
    expect(stored[0]).toMatchObject({ correct: 3, total: 5, seconds: 30 });
    expect(screen.getByRole("table", { name: "Drill history" })).toBeTruthy();
  });

  it("shows earlier sessions on a fresh visit", () => {
    storage.setItem(
      storageKey,
      JSON.stringify([
        {
          seed: 1,
          at: "2026-09-06T07:00:00.000Z",
          total: 20,
          correct: 17,
          seconds: 512,
          byKind: {
            db: { total: 4, correct: 4 },
            wavelength: { total: 4, correct: 4 },
            dipole: { total: 4, correct: 3 },
            ohm: { total: 4, correct: 4 },
            subnet: { total: 4, correct: 2 },
          },
        },
      ]),
    );

    render(<DrillPanel seed={11} count={5} now={ticking()} />);

    const table = screen.getByRole("table", { name: "Drill history" });
    expect(table.textContent).toContain("2026-09-06");
    expect(table.textContent).toContain("17/20");
    expect(table.textContent).toContain("8:32");
    expect(table.textContent).toContain("Subnetting");
  });

  it("ignores stored history it cannot read", () => {
    storage.setItem(storageKey, "{not json");
    render(<DrillPanel seed={11} count={5} now={ticking()} />);
    expect(screen.queryByRole("table", { name: "Drill history" })).toBeNull();
  });
});
