import { describe, expect, it } from "vitest";
import {
  emptyTraining,
  noSolves,
  startCheckpoint,
  type TrainingDocument,
  toggleItem,
  unitStates,
} from "../progress";
import { syllabus } from "../syllabus";
import {
  type DueReview,
  defaultSession,
  type Entry,
  interleave,
  nextUp,
  trackMinutes,
  weekOfPhase,
} from "../today";

const now = new Date("2026-11-10T08:00:00Z");
const unit = (id: string) => {
  const found = syllabus.units.find((candidate) => candidate.id === id);
  if (!found) throw new Error(id);
  return found;
};
const tracks = (entries: Entry[]) =>
  entries.map((entry) =>
    entry.kind === "review" ? "review" : entry.unit.track,
  );
const ids = (entries: Entry[]) =>
  entries.map((entry) =>
    entry.kind === "item"
      ? entry.item.id
      : entry.kind === "checkpoint"
        ? `checkpoint:${entry.unit.id}`
        : entry.review.key,
  );

/** Every checkpoint item ticked inside a fresh attempt: the unit is placed out. */
function placeOut(document: TrainingDocument, id: string): TrainingDocument {
  let next = startCheckpoint(document, id, now);
  for (const check of unit(id).checkpoint?.items ?? [])
    next = toggleItem(next, check.id, now);
  return next;
}

/** Every item of a unit ticked. */
function finish(document: TrainingDocument, id: string): TrainingDocument {
  let next = document;
  for (const lesson of unit(id).lessons)
    for (const each of lesson.items) next = toggleItem(next, each.id, now);
  return next;
}

/** The C++ checkpoint failed and the AMC baseline done: lessons in both tracks. */
function underway(): TrainingDocument {
  const before = new Date("2026-11-01T08:00:00Z");
  let document = startCheckpoint(emptyTraining(), "cpp-basics", before);
  document = finish(document, "amc-baseline");
  return startCheckpoint(document, "prealgebra-1", before);
}

describe("the session length", () => {
  it("defaults to the plan's self-paced minutes a week over its sessions", () => {
    // Phase 1: math 60 + 60, programming 60. Three sessions of 60.
    expect(trackMinutes(syllabus.phases[1])).toEqual({
      math: 120,
      programming: 60,
    });
    expect(defaultSession(syllabus.phases[1])).toBe(60);
    // Phase 0: math 60, programming 60. Two sessions of 60.
    expect(defaultSession(syllabus.phases[0])).toBe(60);
  });
});

describe("next up", () => {
  it("is the same list any day of the week", () => {
    const lists = ["2026-11-09", "2026-11-10", "2026-11-13", "2026-11-14"].map(
      (date) =>
        ids(
          nextUp(date, emptyTraining(), noSolves, now, { session: 600 })
            .entries,
        ),
    );
    for (const list of lists) expect(list).toEqual(lists[0]);
    expect(lists[0]).toContain("refresh-learncpp-ints");
    expect(lists[0]).toContain("refresh-alcumus-setup");
  });

  it("interleaves the tracks by the plan's split of minutes", () => {
    const queue = (track: "math" | "programming", n: number): Entry[] => {
      const from = unit(track === "math" ? "cp-counting" : "bronze-1");
      return Array.from({ length: n }, (_, index) => ({
        kind: "item" as const,
        minutes: 30,
        unit: from,
        lesson: from.lessons[0],
        item: { ...from.lessons[0].items[0], id: `${track}-${index}` },
      }));
    };
    const merged = interleave(
      { math: queue("math", 6), programming: queue("programming", 6) },
      { math: 120, programming: 60 },
    );
    // Two math for each programming while both last.
    expect(tracks(merged).slice(0, 6)).toEqual([
      "math",
      "programming",
      "math",
      "math",
      "programming",
      "math",
    ]);
    expect(merged).toHaveLength(12);
  });

  it("fills one session, and Keep going adds one more each time without limit", () => {
    const document = underway();
    const base = nextUp("2027-01-05", document, noSolves, now, {
      session: 60,
    });
    expect(base.entries.length).toBeGreaterThan(0);
    expect(base.later).toBeGreaterThan(0);
    const more = nextUp("2027-01-05", document, noSolves, now, {
      session: 60,
      more: 1,
    });
    expect(more.entries).toHaveLength(base.entries.length + 1);
    expect(ids(more.entries).slice(0, base.entries.length)).toEqual(
      ids(base.entries),
    );
    const lots = nextUp("2027-01-05", document, noSolves, now, {
      session: 60,
      more: 10_000,
    });
    expect(lots.later).toBe(0);
    expect(lots.entries.length).toBe(base.entries.length + base.later);
  });

  it("shows the first item even when it alone is longer than the session", () => {
    const next = nextUp("2026-11-10", underway(), noSolves, now, {
      session: 10,
    });
    expect(next.entries).toHaveLength(1);
  });

  it("is the same after a three-week gap: nothing piles up", () => {
    const document = underway();
    const before = nextUp("2027-01-05", document, noSolves, now);
    const after = nextUp("2027-01-26", document, noSolves, now);
    expect(ids(after.entries)).toEqual(ids(before.entries));
    expect(after.planned).toBe(before.planned);
  });

  it("never schedules a placed-out unit or the contest unit", () => {
    const placed = placeOut(emptyTraining(), "cpp-basics");
    const next = nextUp("2026-11-10", placed, noSolves, now, { more: 50 });
    for (const entry of next.entries) {
      if (entry.kind === "review") continue;
      expect(entry.unit.id).not.toBe("cpp-basics");
      expect(entry.unit.slot).toBeUndefined();
    }
  });

  it("puts reviews first", () => {
    const review: DueReview = {
      key: "lesson:pre-1:2",
      track: "math",
      title: "Chapter 1: Properties of Arithmetic",
      minutes: 10,
      unit: unit("prealgebra-1"),
      due: "2026-11-12",
      from: "2026-11-10",
    };
    const next = nextUp("2026-11-12", emptyTraining(), noSolves, now, {
      reviews: [review],
    });
    expect(next.entries[0].kind).toBe("review");
  });

  it("names the phase's week by its target dates", () => {
    expect(weekOfPhase(syllabus.phases[1], "2027-01-28")).toBe(4);
    const next = nextUp("2026-11-10", emptyTraining(), noSolves, now);
    expect(next.phase.phase).toBe(0);
    expect(next.week).toBe(1);
    expect(next.weeks).toBe(8);
  });
});

describe("unlocks", () => {
  it("open by the unit before finishing or placing out, never by date", () => {
    let refreshed = emptyTraining();
    for (const id of [
      "refresh-cpp",
      "refresh-stl",
      "refresh-complexity",
      "cpp-toolkit",
      "refresh-dsa",
    ])
      refreshed = finish(refreshed, id);
    const placed = placeOut(refreshed, "cpp-basics");
    for (const moment of [
      new Date("2026-11-10T08:00:00Z"),
      new Date("2031-01-01T08:00:00Z"),
    ]) {
      expect(
        unitStates(emptyTraining(), noSolves, moment).get("cpp-basics")?.state,
      ).toBe("waiting");
      expect(
        unitStates(refreshed, noSolves, moment).get("cpp-basics")?.state,
      ).toBe("open");
      // A Phase 1 unit opens in Phase 0 once the unit before it is placed out.
      const after = unitStates(placed, noSolves, moment);
      expect(after.get("bronze-1")?.state).toBe("open");
      expect(after.get("band-900")?.state).toBe("waiting");
    }
  });

  it("say when a track reaches its last written unit", () => {
    let document = emptyTraining();
    const programming = syllabus.units.filter(
      (each) => each.track === "programming" && !each.locked && !each.slot,
    );
    for (const each of programming.slice(0, -1))
      document = each.checkpoint
        ? placeOut(document, each.id)
        : finish(document, each.id);
    const next = nextUp("2027-06-01", document, noSolves, now);
    expect(next.lastWritten).toContain("programming");
    expect(
      nextUp("2027-06-01", emptyTraining(), noSolves, now).lastWritten,
    ).toEqual([]);
  });
});
