import { describe, expect, it } from "vitest";
import {
  canTick,
  checkpointState,
  emptyTraining,
  itemStatus,
  normalizeTraining,
  noSolves,
  overall,
  passDrill,
  type Solves,
  setCode,
  setMissed,
  setScore,
  startCheckpoint,
  stopCheckpoint,
  type TrainingDocument,
  toggleItem,
  unitStates,
} from "../progress";
import { locate, syllabus, type Unit } from "../syllabus";

const at = (iso: string) => new Date(iso);
const unit = (id: string): Unit => {
  const found = syllabus.units.find((candidate) => candidate.id === id);
  if (!found) throw new Error(id);
  return found;
};
const item = (id: string) => {
  const found = locate(id)?.item;
  if (!found) throw new Error(id);
  return found;
};

describe("the saved document", () => {
  it("is made whole from nothing, with the plan's Codeforces handle", () => {
    const document = normalizeTraining(null);
    expect(document).toEqual(emptyTraining());
    expect(document.settings.codeforces).toBe("Nebikinizer");
  });

  it("keeps what is well formed and drops what is not", () => {
    const document = normalizeTraining({
      items: {
        good: { at: "2026-11-10T10:00:00.000Z", missed: true, score: 96 },
        bad: { at: "yesterday" },
        worse: 3,
      },
      checkpoints: {
        "bronze-1": [{ started: "2026-11-10T10:00:00.000Z" }, { started: 1 }],
      },
      reviews: { "lesson:x:2": "2026-11-12T10:00:00.000Z", junk: "no" },
      notes: { "cp-1": { text: "casework", at: "2026-11-10T10:00:00.000Z" } },
      settings: { codeforces: "someone", atcoder: "me" },
    });
    expect(document.items).toEqual({
      good: { at: "2026-11-10T10:00:00.000Z", missed: true, score: 96 },
    });
    expect(document.checkpoints["bronze-1"]).toHaveLength(1);
    expect(Object.keys(document.reviews)).toEqual(["lesson:x:2"]);
    expect(document.notes["cp-1"].text).toBe("casework");
    expect(document.settings).toEqual({ codeforces: "someone", atcoder: "me" });
  });
});

describe("an item", () => {
  const now = at("2026-11-10T10:00:00Z");

  it("is ticked and unticked by hand", () => {
    const once = toggleItem(emptyTraining(), "cp-1-read", now);
    expect(itemStatus(item("cp-1-read"), once)).toMatchObject({
      done: true,
      by: "manual",
      at: now.toISOString(),
    });
    const twice = toggleItem(once, "cp-1-read", now);
    expect(itemStatus(item("cp-1-read"), twice).done).toBe(false);
  });

  it("marked missed is done with misses, and a score is kept", () => {
    let document = setMissed(emptyTraining(), "amc10-check", true, now);
    expect(itemStatus(item("amc10-check"), document)).toMatchObject({
      done: true,
      missed: true,
    });
    document = setMissed(document, "amc10-check", false, now);
    expect(itemStatus(item("amc10-check"), document).missed).toBe(false);
    document = setScore(document, "amc10-sit", 87, now);
    expect(itemStatus(item("amc10-sit"), document).score).toBe(87);
  });

  it("on Codeforces counts only when the API sees it solved", () => {
    const problem = item("cf-2263B");
    const solves: Solves = {
      codeforces: { solved: { "2263B": "2026-11-10T09:40:00.000Z" } },
      atcoder: null,
    };
    expect(itemStatus(problem, emptyTraining(), solves)).toMatchObject({
      done: true,
      by: "codeforces",
      verified: true,
      at: "2026-11-10T09:40:00.000Z",
    });
    const unsolved: Solves = {
      codeforces: { solved: {} },
      atcoder: null,
    };
    expect(itemStatus(problem, emptyTraining(), unsolved).done).toBe(false);
    expect(canTick(problem, unsolved)).toBe(false);
  });

  it("on Codeforces is ticked by hand only while the API is down", () => {
    expect(canTick(item("cf-2263B"), noSolves)).toBe(true);
    expect(canTick(item("cp-1-read"), noSolves)).toBe(true);
  });

  it("for the first rated contest is done once the API lists one", () => {
    const contest = item("first-rated-contest");
    const solves: Solves = {
      codeforces: { solved: {}, ratedContests: 1 },
      atcoder: null,
    };
    expect(itemStatus(contest, emptyTraining(), solves).done).toBe(true);
    expect(
      itemStatus(contest, emptyTraining(), {
        codeforces: { solved: {}, ratedContests: 0 },
        atcoder: null,
      }).done,
    ).toBe(false);
  });

  it("on AtCoder reads the AtCoder Problems answer", () => {
    const problem = item("ac-abc482_c");
    const solves: Solves = {
      codeforces: null,
      atcoder: { solved: { abc482_c: "2026-11-10T09:00:00.000Z" } },
    };
    expect(itemStatus(problem, emptyTraining(), solves).by).toBe("atcoder");
  });
});

describe("a drill", () => {
  const now = at("2026-11-10T10:00:00Z");

  it("is done only by passing, never by a tick", () => {
    const drill = item("drill-sum");
    expect(canTick(drill, noSolves)).toBe(false);
    const passed = passDrill(emptyTraining(), "drill-sum", now);
    expect(itemStatus(drill, passed)).toMatchObject({
      done: true,
      by: "drill",
    });
    expect(passDrill(passed, "drill-sum", at("2026-11-11T10:00:00Z"))).toBe(
      passed,
    );
  });

  it("keeps the latest source", () => {
    const document = setCode(emptyTraining(), "drill-sum", "int main(){}");
    expect(normalizeTraining(document).code["drill-sum"]).toBe("int main(){}");
  });
});

describe("a checkpoint", () => {
  const start = at("2026-11-10T10:00:00Z");
  const bronze = unit("bronze-1");
  const solvedAt = (minutes: number) =>
    new Date(start.getTime() + minutes * 60_000).toISOString();
  const solves = (minutes: number[]): Solves => ({
    codeforces: {
      solved: Object.fromEntries(
        (bronze.checkpoint?.items ?? []).map((check, index) => [
          check.problem?.id,
          solvedAt(minutes[index]),
        ]),
      ),
    },
    atcoder: null,
  });

  it("is not started until Start is pressed", () => {
    expect(
      checkpointState(bronze, emptyTraining(), noSolves, start).state,
    ).toBe("none");
  });

  it("is running until its time is up", () => {
    const document = startCheckpoint(emptyTraining(), "bronze-1", start);
    const state = checkpointState(
      bronze,
      document,
      noSolves,
      new Date(start.getTime() + 30 * 60_000),
    );
    expect(state).toEqual({
      state: "running",
      started: start.toISOString(),
      ends: solvedAt(60),
    });
  });

  it("passes when every problem is solved within the time", () => {
    const document = startCheckpoint(emptyTraining(), "bronze-1", start);
    const state = checkpointState(
      bronze,
      document,
      solves([10, 25, 59]),
      at("2026-11-10T11:30:00Z"),
    );
    expect(state).toEqual({
      state: "passed",
      started: start.toISOString(),
      at: solvedAt(59),
    });
  });

  it("fails when one is solved after the time", () => {
    const document = startCheckpoint(emptyTraining(), "bronze-1", start);
    expect(
      checkpointState(
        bronze,
        document,
        solves([10, 25, 61]),
        at("2026-11-10T11:30:00Z"),
      ).state,
    ).toBe("failed");
  });

  it("fails at once when stopped, and a new attempt can pass", () => {
    let document = startCheckpoint(emptyTraining(), "bronze-1", start);
    document = stopCheckpoint(document, "bronze-1", at("2026-11-10T10:05:00Z"));
    expect(
      checkpointState(bronze, document, noSolves, at("2026-11-10T10:06:00Z"))
        .state,
    ).toBe("failed");
    const again = at("2026-11-11T10:00:00Z");
    document = startCheckpoint(document, "bronze-1", again);
    const late: Solves = {
      codeforces: {
        solved: Object.fromEntries(
          (bronze.checkpoint?.items ?? []).map((check) => [
            check.problem?.id,
            "2026-11-11T10:30:00.000Z",
          ]),
        ),
      },
      atcoder: null,
    };
    expect(checkpointState(bronze, document, late, again).state).toBe("passed");
  });

  it("does not pass on a manual set marked missed", () => {
    const pre = unit("prealgebra-1");
    let document: TrainingDocument = startCheckpoint(
      emptyTraining(),
      "prealgebra-1",
      start,
    );
    for (const check of pre.checkpoint?.items ?? [])
      document = toggleItem(document, check.id, at("2026-11-10T10:20:00Z"));
    expect(checkpointState(pre, document, noSolves, start).state).toBe(
      "passed",
    );
    const first = pre.checkpoint?.items[0].id ?? "";
    document = setMissed(document, first, true, at("2026-11-10T10:20:00Z"));
    expect(
      checkpointState(pre, document, noSolves, at("2026-11-10T12:00:00Z"))
        .state,
    ).toBe("failed");
  });
});

describe("the units", () => {
  const now = at("2026-11-10T10:00:00Z");

  it("open one at a time in each track", () => {
    const states = unitStates(emptyTraining(), noSolves, now);
    expect(states.get("refresh-cpp")?.state).toBe("open");
    expect(states.get("refresh-stl")).toMatchObject({ state: "waiting" });
    expect(states.get("cpp-basics")).toMatchObject({ state: "waiting" });
    expect(states.get("refresh-algebra")?.state).toBe("open");
    expect(states.get("amc-baseline")).toMatchObject({ state: "waiting" });
    expect(states.get("p3-chen")?.state).toBe("locked");
  });

  it("hold nothing up behind the first rated contest", () => {
    const states = unitStates(emptyTraining(), noSolves, now);
    expect(states.get("first-contest")?.state).toBe("open");
  });

  it("place out on a passed checkpoint, and the next one opens", () => {
    let document = startCheckpoint(emptyTraining(), "cpp-basics", now);
    for (const check of unit("cpp-basics").checkpoint?.items ?? [])
      document = toggleItem(document, check.id, at("2026-11-10T10:20:00Z"));
    const states = unitStates(document, noSolves, now);
    expect(states.get("cpp-basics")).toEqual({
      state: "placed",
      at: "2026-11-10T10:20:00.000Z",
      started: now.toISOString(),
      by: "checkpoint",
    });
    expect(states.get("bronze-1")?.state).toBe("open");
  });

  it("count a placed-out unit whole in the overall progress", () => {
    let document = startCheckpoint(emptyTraining(), "cpp-basics", now);
    for (const check of unit("cpp-basics").checkpoint?.items ?? [])
      document = toggleItem(document, check.id, at("2026-11-10T10:20:00Z"));
    const states = unitStates(document, noSolves, now);
    const count = overall(document, noSolves, states);
    const basics = unit("cpp-basics").lessons.flatMap((lesson) => lesson.items);
    expect(count.done).toBe(basics.length);
  });

  it("finish when every item is done", () => {
    let document = emptyTraining();
    for (const lesson of unit("cpp-basics").lessons)
      for (const each of lesson.items)
        document = toggleItem(document, each.id, now);
    expect(unitStates(document, noSolves, now).get("cpp-basics")?.state).toBe(
      "done",
    );
  });
});
