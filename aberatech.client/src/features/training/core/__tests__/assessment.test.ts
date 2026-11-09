import { beforeAll, describe, expect, it } from "vitest";
import {
  type Answer,
  ankiCsv,
  checkKey,
  gradeCard,
  gradeChoice,
  gradeNumber,
  gradeProof,
  masteryOpen,
  masteryState,
  placementState,
  quizQuestions,
  recordAttempt,
  referenceUrl,
  setExternal,
} from "../assessment";
import { applyFixture, type Fixture } from "../fixture";
import {
  emptyTraining,
  lessonDone,
  normalizeTraining,
  noSolves,
  type TrainingDocument,
  toggleItem,
  unitStates,
} from "../progress";
import { dueOn, questionKey, reviewSources } from "../reviews";
import { type Question, syllabus } from "../syllabus";
import { nextUp } from "../today";

const at = (date: string) => new Date(`${date}T12:00:00Z`);
const day0 = "2026-11-10";
const ref = {
  label: "USACO Guide",
  url: "https://usaco.guide/general/data-types",
};

const choice: Question = {
  id: "t-choice",
  type: "choice",
  prompt: "Pick B.",
  choices: ["A", "B", "C"],
  answer: 1,
  why: "B is the one.",
  ref,
};
const number: Question = {
  id: "t-number",
  type: "number",
  prompt: "2 to the 10th?",
  answer: "1024",
  why: "Ten doublings.",
  ref,
};
const proof: Question = {
  id: "t-proof",
  type: "proof",
  prompt: "Prove it.",
  rubric: ["States the claim", "Covers every case"],
  why: "Two cases.",
  ref: {
    label: "AoPS video",
    url: "https://www.youtube.com/watch?v=abc",
    at: 125,
  },
};
const card: Question = {
  id: "t-card",
  type: "card",
  prompt: "Front",
  back: 'Back, with a "quote", and a comma',
  why: "Recall.",
  ref,
};
const quizOnly: Question = { ...choice, id: "t-quiz" };

// Ten mastery questions, so one wrong is 90% and two are 80%.
const many: Question[] = Array.from({ length: 10 }, (_, i) => ({
  ...number,
  id: `t-m${i}`,
}));

const fixture: Fixture = {
  lessons: {
    "refresh-cpp-lambdas": [choice, number, proof, card, quizOnly, ...many],
  },
  units: {
    "refresh-cpp": {
      pretest: ["t-choice", "t-number"],
      mastery: many.map((q) => q.id),
    },
    "amc-baseline": {
      placement: {
        label: "AoPS Prealgebra diagnostic",
        url: "https://artofproblemsolving.com/school/course/diagnostic",
        what: "Take the diagnostic.",
        minutes: 45,
        pass: 16,
        outOf: 20,
      },
      externalMastery: {
        label: "Alcumus",
        url: "https://artofproblemsolving.com/alcumus",
        what: "Master the topics.",
        minutes: 60,
        topics: ["Fractions", "Ratios"],
      },
    },
  },
};

beforeAll(() => {
  applyFixture(fixture);
});

const unit = (id: string) => {
  const found = syllabus.units.find((each) => each.id === id);
  if (!found) throw new Error(id);
  return found;
};

function attempt(
  document: TrainingDocument,
  key: string,
  when: string,
  answers: Record<string, Answer>,
) {
  return recordAttempt(document, key, { at: at(when).toISOString(), answers });
}

/** Answers to the ten mastery questions with `wrong` of them wrong. */
const mastery = (wrong: number): Record<string, Answer> =>
  Object.fromEntries(
    many.map((q, i) => [q.id, { correct: i >= wrong, given: "x" }]),
  );

describe("grading", () => {
  it("grades a choice by its index and a number by value", () => {
    expect(gradeChoice(choice as never, 1).correct).toBe(true);
    expect(gradeChoice(choice as never, 0).correct).toBe(false);
    expect(gradeNumber(number as never, "2^10").correct).toBe(true);
    expect(gradeNumber(number as never, "1000").correct).toBe(false);
  });

  it("passes a proof only when every correctness line is met", () => {
    expect(
      gradeProof("p", { met: [true, true], elegant: false, clear: false })
        .correct,
    ).toBe(true);
    expect(
      gradeProof("p", { met: [true, false], elegant: true, clear: true })
        .correct,
    ).toBe(false);
  });

  it("counts a card right at Hard or better", () => {
    expect(gradeCard(1).correct).toBe(false);
    expect(gradeCard(2).correct).toBe(true);
  });

  it("links a video reference at its time", () => {
    expect(referenceUrl(proof.ref)).toBe(
      "https://www.youtube.com/watch?v=abc&t=125",
    );
    expect(referenceUrl(ref)).toBe(ref.url);
  });

  it("keeps attempts through the server's JSON", () => {
    const document = attempt(
      emptyTraining(),
      "quiz:refresh-cpp-lambdas",
      day0,
      {
        "t-proof": gradeProof("text", {
          met: [true, true],
          elegant: true,
          clear: false,
        }),
        "t-card": gradeCard(3),
      },
    );
    const back = normalizeTraining(JSON.parse(JSON.stringify(document)));
    expect(back.assessments).toEqual(document.assessments);
  });
});

describe("the mastery rule", () => {
  const basics = unit("refresh-cpp");

  it("closes the unit at 90%", () => {
    const document = attempt(
      emptyTraining(),
      checkKey("mastery", "refresh-cpp"),
      day0,
      mastery(1),
    );
    expect(masteryState(basics, document).state).toBe("passed");
    expect(unitStates(document, noSolves, at(day0)).get("refresh-cpp")).toEqual(
      {
        state: "done",
        by: "mastery",
      },
    );
    expect(
      unitStates(document, noSolves, at(day0)).get("refresh-stl")?.state,
    ).toBe("open");
  });

  it("does not close it at 80%, and opens the retest 2 days later", () => {
    const document = attempt(
      emptyTraining(),
      checkKey("mastery", "refresh-cpp"),
      day0,
      mastery(2),
    );
    const state = masteryState(basics, document);
    expect(state.state).toBe("failed");
    expect(state.state === "failed" && state.opens).toBe("2026-11-12");
    expect(masteryOpen(basics, document, "2026-11-11")).toBe(false);
    expect(masteryOpen(basics, document, "2026-11-12")).toBe(true);
    expect(
      unitStates(document, noSolves, at(day0)).get("refresh-cpp")?.state,
    ).toBe("open");
  });

  it("keeps a unit with a mastery check open when its lessons are done", () => {
    let document = emptyTraining();
    for (const lesson of basics.lessons)
      for (const item of lesson.items)
        document = toggleItem(document, item.id, at(day0));
    document = attempt(
      document,
      checkKey("quiz", "refresh-cpp-lambdas"),
      day0,
      {},
    );
    expect(
      basics.lessons.every((lesson) => lessonDone(lesson, document, noSolves)),
    ).toBe(true);
    expect(
      unitStates(document, noSolves, at(day0)).get("refresh-cpp")?.state,
    ).toBe("open");
  });

  it("lists the check in Next up when the lessons are done, and again after the wait", () => {
    let document = emptyTraining();
    // The pretest taken and not passed.
    document = attempt(document, checkKey("pretest", "refresh-cpp"), day0, {
      "t-choice": { correct: false },
    });
    for (const lesson of basics.lessons)
      for (const item of lesson.items)
        document = toggleItem(document, item.id, at(day0));
    document = attempt(
      document,
      checkKey("quiz", "refresh-cpp-lambdas"),
      day0,
      {},
    );
    const checks = (doc: TrainingDocument, date: string) =>
      nextUp(date, doc, noSolves, at(date), { more: 99 }).entries.filter(
        (entry) => entry.kind === "check" && entry.unit.id === "refresh-cpp",
      );
    expect(
      checks(document, day0).map((e) => e.kind === "check" && e.check),
    ).toEqual(["mastery"]);

    document = attempt(
      document,
      checkKey("mastery", "refresh-cpp"),
      day0,
      mastery(5),
    );
    expect(checks(document, "2026-11-11")).toHaveLength(0);
    expect(
      nextUp("2026-11-11", document, noSolves, at("2026-11-11")).retests,
    ).toEqual([{ unit: basics, opens: "2026-11-12" }]);
    expect(checks(document, "2026-11-12")).toHaveLength(1);
  });

  it("closes a unit by an outside mastery check ticked by hand", () => {
    const amc = unit("amc-baseline");
    const document = setExternal(emptyTraining(), "mastery:amc-baseline", {
      at: at(day0).toISOString(),
    });
    expect(masteryState(amc, document)).toEqual({
      state: "passed",
      at: at(day0).toISOString(),
      by: "outside",
    });
    expect(
      unitStates(document, noSolves, at(day0)).get("amc-baseline")?.state,
    ).toBe("done");
    // Its lessons enter review, as after a passed check.
    const keys = reviewSources(document, noSolves).map((s) => s.key);
    for (const lesson of amc.lessons)
      expect(keys).toContain(`lesson:${lesson.id}`);
  });
});

describe("placement", () => {
  it("places out on a pretest at 90% or more, and not below", () => {
    const basics = unit("refresh-cpp");
    const key = checkKey("pretest", "refresh-cpp");
    const passed = attempt(emptyTraining(), key, day0, {
      "t-choice": { correct: true },
      "t-number": { correct: true },
    });
    expect(
      unitStates(passed, noSolves, at(day0)).get("refresh-cpp"),
    ).toMatchObject({
      state: "placed",
      by: "pretest",
    });
    const failed = attempt(emptyTraining(), key, day0, {
      "t-choice": { correct: true },
      "t-number": { correct: false },
    });
    expect(placementState(basics, failed).state).toBe("failed");
    expect(
      unitStates(failed, noSolves, at(day0)).get("refresh-cpp")?.state,
    ).toBe("open");
  });

  it("puts an untaken pretest first in its unit in Next up", () => {
    const next = nextUp(day0, emptyTraining(), noSolves, at(day0), {
      more: 99,
    });
    const first = next.entries.find(
      (entry) => entry.kind !== "review" && entry.unit.id === "refresh-cpp",
    );
    expect(first?.kind === "check" && first.check).toBe("pretest");
  });

  it("places out on an outside diagnostic at its pass mark", () => {
    const amc = unit("amc-baseline");
    const scored = (score: number) =>
      setExternal(emptyTraining(), "placement:amc-baseline", {
        at: at(day0).toISOString(),
        score,
      });
    expect(placementState(amc, scored(16)).state).toBe("passed");
    expect(
      unitStates(scored(16), noSolves, at(day0)).get("amc-baseline"),
    ).toMatchObject({
      state: "placed",
      by: "placement",
    });
    expect(placementState(amc, scored(15)).state).toBe("failed");
  });
});

describe("lesson quizzes", () => {
  const lesson = unit("refresh-cpp").lessons.find(
    (each) => each.id === "refresh-cpp-lambdas",
  );
  if (!lesson) throw new Error("refresh-cpp-lambdas");

  it("leave out the questions the unit holds for its checks", () => {
    expect(quizQuestions(lesson).map((q) => q.id)).toEqual([
      "t-proof",
      "t-card",
      "t-quiz",
    ]);
  });

  it("must be taken before the lesson is done", () => {
    let document = emptyTraining();
    for (const item of lesson.items)
      document = toggleItem(document, item.id, at(day0));
    expect(lessonDone(lesson, document, noSolves)).toBe(false);
    document = attempt(
      document,
      checkKey("quiz", "refresh-cpp-lambdas"),
      day0,
      {
        "t-proof": { correct: false },
      },
    );
    expect(lessonDone(lesson, document, noSolves)).toBe(true);
  });

  it("send a wrong answer and every card to review, and not a right answer", () => {
    const document = attempt(
      emptyTraining(),
      checkKey("quiz", "refresh-cpp-lambdas"),
      day0,
      {
        "t-quiz": { correct: false, given: "0" },
        "t-proof": { correct: true },
        "t-card": gradeCard(3),
      },
    );
    const keys = reviewSources(document, noSolves).map((s) => s.key);
    expect(keys).toContain(questionKey("t-quiz"));
    expect(keys).toContain(questionKey("t-card"));
    expect(keys).not.toContain(questionKey("t-proof"));
    // The wrong answer is back the next day, in relearning.
    const due = dueOn("2026-11-11", document, noSolves).find(
      (entry) => entry.source.key === questionKey("t-quiz"),
    );
    expect(due?.card.relearning).toBeDefined();
  });
});

describe("the Anki export", () => {
  it("writes a header Anki reads and one quoted line per card", () => {
    const { text, cards } = ankiCsv();
    expect(cards).toBe(1);
    const lines = text.trimEnd().split("\n");
    expect(lines.slice(0, 5)).toEqual([
      "#separator:Comma",
      "#html:false",
      "#notetype:Basic",
      "#deck:abera.tech training",
      "#tags column:3",
    ]);
    expect(lines[5]).toBe(
      '"Front","Back, with a ""quote"", and a comma","refresh_cpp refresh_cpp_lambdas"',
    );
  });
});
