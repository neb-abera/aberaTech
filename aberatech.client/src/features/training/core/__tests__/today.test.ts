import { describe, expect, it } from "vitest";
import {
  emptyTraining,
  noSolves,
  startCheckpoint,
  type TrainingDocument,
  toggleItem,
} from "../progress";
import { syllabus } from "../syllabus";
import { type DueReview, type Entry, today, weekOfPhase } from "../today";

const now = new Date("2026-11-10T08:00:00Z");
const kinds = (entries: Entry[]) => entries.map((entry) => entry.kind);
const unit = (id: string) => {
  const found = syllabus.units.find((candidate) => candidate.id === id);
  if (!found) throw new Error(id);
  return found;
};

/** Every checkpoint item ticked inside a fresh attempt: the unit is placed out. */
function placeOut(document: TrainingDocument, id: string): TrainingDocument {
  let next = startCheckpoint(document, id, now);
  for (const check of unit(id).checkpoint?.items ?? [])
    next = toggleItem(next, check.id, now);
  return next;
}

describe("today", () => {
  it("in Phase 0 starts each track with its first checkpoint", () => {
    // Tue 10 Nov 2026: Phase 0's programming block.
    const tuesday = today("2026-11-10", emptyTraining(), noSolves, now);
    expect(tuesday.phase.phase).toBe(0);
    expect(tuesday.block?.block).toBe("programming");
    expect(tuesday.entries).toHaveLength(1);
    expect(tuesday.entries[0]).toMatchObject({
      kind: "checkpoint",
      minutes: 30,
      unit: { id: "cpp-basics" },
    });
  });

  it("goes to the lessons, in order, once the checkpoint was not passed", () => {
    const failed = startCheckpoint(
      emptyTraining(),
      "cpp-basics",
      new Date("2026-11-03T08:00:00Z"),
    );
    const tuesday = today("2026-11-10", failed, noSolves, now);
    expect(kinds(tuesday.entries)).toEqual(["item", "item", "item"]);
    // 15 + 20 + 20 = 55 fits in 60. The next, 15, would make 70.
    expect(tuesday.planned).toBe(55);
    expect(tuesday.later).toBe(
      unit("cpp-basics").lessons.flatMap((lesson) => lesson.items).length - 3,
    );
  });

  it("moves to the next unit once a unit is placed out, and never schedules it", () => {
    const placed = placeOut(emptyTraining(), "cpp-basics");
    const tuesday = today("2026-11-10", placed, noSolves, now);
    const ids = tuesday.entries.flatMap((entry) =>
      entry.kind === "item" ? [entry.unit.id] : [],
    );
    expect(ids.every((id) => id === "cpp-toolkit")).toBe(true);
  });

  it("shows the first item even when it alone is longer than the block", () => {
    // Thu 12 Nov: math, 60 minutes. The AMC 10 baseline's first item is 10
    // minutes, then the 75 minute paper would make 85: only one fits.
    const thursday = today("2026-11-12", emptyTraining(), noSolves, now);
    expect(thursday.block?.block).toBe("math");
    expect(thursday.entries).toHaveLength(1);
    const paper = toggleItem(emptyTraining(), "alcumus-account", now);
    const next = today("2026-11-12", paper, noSolves, now);
    expect(next.entries).toHaveLength(1);
    expect(next.planned).toBe(75);
  });

  it("puts the first rated contest on contest days until it is entered", () => {
    // Sat 14 Nov.
    const saturday = today("2026-11-14", emptyTraining(), noSolves, now);
    expect(saturday.block?.block).toBe("contest");
    expect(saturday.entries[0]).toMatchObject({
      kind: "item",
      item: { id: "usaco-contest-strategy" },
    });
    const entered = toggleItem(
      toggleItem(emptyTraining(), "usaco-contest-strategy", now),
      "first-rated-contest",
      now,
    );
    expect(kinds(today("2026-11-14", entered, noSolves, now).entries)).toEqual([
      "contest",
    ]);
  });

  it("on a day with no block shows the next one", () => {
    // Mon 9 Nov has no block. Tuesday is next.
    const monday = today("2026-11-09", emptyTraining(), noSolves, now);
    expect(monday.block).toBeNull();
    expect(monday.entries).toEqual([]);
    expect(monday.next?.date).toBe("2026-11-10");
    expect(monday.next?.entries[0].kind).toBe("checkpoint");
  });

  it("follows Phase 1's week from the plan", () => {
    expect(
      today("2027-01-02", emptyTraining(), noSolves, now).block?.block,
    ).toBe("contest"); // Saturday
    expect(
      today("2027-01-03", emptyTraining(), noSolves, now).block?.block,
    ).toBe("upsolve"); // Sunday
    expect(
      today("2027-01-05", emptyTraining(), noSolves, now).block?.block,
    ).toBe("math"); // Tuesday
    expect(
      today("2027-01-06", emptyTraining(), noSolves, now).block?.block,
    ).toBe("programming"); // Wednesday
    expect(
      today("2027-01-04", emptyTraining(), noSolves, now).block,
    ).toBeNull(); // Monday
  });

  it("makes every fourth Thursday of Phase 1 a mixed set", () => {
    // Phase 1 starts Fri 1 Jan 2027. Thu 28 Jan is in week 4.
    const phase = syllabus.phases[1];
    expect(weekOfPhase(phase, "2027-01-28")).toBe(4);
    expect(
      kinds(today("2027-01-28", emptyTraining(), noSolves, now).entries),
    ).toEqual(["mixed"]);
    expect(
      kinds(today("2027-01-21", emptyTraining(), noSolves, now).entries),
    ).not.toContain("mixed");
  });

  it("takes reviews due in the block's track first, then the work", () => {
    const review: DueReview = {
      key: "lesson:pre-1:2",
      track: "math",
      title: "Chapter 1: Properties of Arithmetic",
      minutes: 10,
      unit: unit("prealgebra-1"),
      due: "2026-11-12",
      from: "2026-11-10",
    };
    const other: DueReview = { ...review, key: "x", track: "programming" };
    const thursday = today("2026-11-12", emptyTraining(), noSolves, now, [
      review,
      other,
    ]);
    expect(kinds(thursday.entries)).toEqual(["review", "item"]);
  });
});
