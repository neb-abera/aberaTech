/**
 * The operations every plan's progress is made of, on the document alone:
 * what a tick, a reset and a gate attempt do to what is saved, and what a
 * malformed row loads as.
 */
import { describe, expect, it } from "vitest";
import {
  addAttempt,
  type Block,
  clearDone,
  emptyCurriculum,
  normaliseCurriculum,
  removeAttempt,
  tasksOf,
  toggleDone,
} from "../curriculum";

describe("normaliseCurriculum", () => {
  it("makes nothing into an empty document", () => {
    expect(normaliseCurriculum(null)).toEqual(emptyCurriculum());
  });

  it("keeps what is well formed and drops what is not", () => {
    const loaded = normaliseCurriculum({
      done: ["a", 3, "b"] as unknown as string[],
      gates: {
        wire: [
          { id: "x", on: "2027-01-05", passed: true, minutes: 27 },
          { garbage: true },
        ],
        // biome-ignore lint/suspicious/noExplicitAny: a malformed row is the point
      } as any,
    });
    expect(loaded.done).toEqual(["a", "b"]);
    expect(loaded.gates.wire).toEqual([
      { id: "x", on: "2027-01-05", passed: true, minutes: 27 },
    ]);
  });

  it("treats a gates value that is not an object as no attempts", () => {
    expect(
      normaliseCurriculum({ gates: [] as unknown as Record<string, never> })
        .gates,
    ).toEqual({});
  });
});

describe("the ticks", () => {
  it("toggle on, toggle off, in order of ticking", () => {
    let document = emptyCurriculum();
    document = toggleDone(document, "b");
    document = toggleDone(document, "a");
    expect(document.done).toEqual(["b", "a"]);
    document = toggleDone(document, "b");
    expect(document.done).toEqual(["a"]);
  });

  it("clear leaves the gate log alone", () => {
    const attempt = { id: "x", on: "2027-01-05", passed: false };
    const document = addAttempt(
      toggleDone(emptyCurriculum(), "a"),
      "wire",
      attempt,
    );
    expect(clearDone(document)).toEqual({
      version: 1,
      done: [],
      gates: { wire: [attempt] },
    });
  });

  it("keeps a plan's extra fields through every operation", () => {
    const document = { ...emptyCurriculum(), drills: [1, 2] };
    expect(toggleDone(document, "a").drills).toEqual([1, 2]);
    expect(clearDone(document).drills).toEqual([1, 2]);
    expect(
      addAttempt(document, "g", { id: "x", on: "2027-01-05", passed: true })
        .drills,
    ).toEqual([1, 2]);
    expect(removeAttempt(document, "g", "x").drills).toEqual([1, 2]);
  });
});

describe("the gate log", () => {
  it("appends per block and removes by id", () => {
    let document = emptyCurriculum();
    document = addAttempt(document, "wire", {
      id: "x",
      on: "2027-01-05",
      passed: false,
    });
    document = addAttempt(document, "wire", {
      id: "y",
      on: "2027-01-19",
      passed: true,
      minutes: 27,
    });
    document = addAttempt(document, "mesh", {
      id: "z",
      on: "2027-03-01",
      passed: true,
    });
    expect(document.gates.wire.map((attempt) => attempt.id)).toEqual([
      "x",
      "y",
    ]);
    document = removeAttempt(document, "wire", "x");
    expect(document.gates.wire.map((attempt) => attempt.id)).toEqual(["y"]);
    expect(document.gates.mesh).toHaveLength(1);
  });

  it("removing from a block with no log is a no-op with an empty log", () => {
    expect(removeAttempt(emptyCurriculum(), "wire", "x").gates).toEqual({
      wire: [],
    });
  });
});

describe("tasksOf", () => {
  it("flattens a plan's tasks in page order", () => {
    const block = (id: string, tasks: string[]): Block => ({
      id,
      title: id,
      weeks: "",
      why: "",
      tasks: tasks.map((task) => ({ id: task, text: task })),
      gate: "",
      resources: [],
      practice: [],
    });
    expect(
      tasksOf([block("a", ["a-1", "a-2"]), block("b", ["b-1"])]).map(
        (task) => task.id,
      ),
    ).toEqual(["a-1", "a-2", "b-1"]);
  });
});
