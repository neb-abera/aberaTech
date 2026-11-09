import { describe, expect, it } from "vitest";
import { addDays } from "../dates";
import { searchNotes, setNote } from "../notes";
import {
  emptyTraining,
  normalizeTraining,
  noSolves,
  setMissed,
  type TrainingDocument,
  toggleItem,
} from "../progress";
import {
  answerReview,
  asDue,
  cardOf,
  dueOn,
  interleaveReviews,
  itemKey,
  lessonKey,
  Rating,
  rate,
  reviewSources,
  type Scheduled,
} from "../reviews";
import { syllabus } from "../syllabus";
import { nextUp } from "../today";

// Noon UTC is the same calendar date in every zone the suite runs in.
const at = (date: string) => new Date(`${date}T12:00:00Z`);
const day0 = "2026-11-10";

const source = (document: TrainingDocument, key: string) => {
  const found = reviewSources(document, noSolves).find((s) => s.key === key);
  if (!found) throw new Error(`no source ${key}`);
  return found;
};

function finishLesson(document: TrainingDocument, id: string, when: Date) {
  const lesson = syllabus.units
    .flatMap((unit) => unit.lessons)
    .find((each) => each.id === id);
  if (!lesson) throw new Error(id);
  let next = document;
  for (const item of lesson.items) next = toggleItem(next, item.id, when);
  return next;
}

describe("FSRS scheduling", () => {
  it("schedules a new card by its first answer, in whole days", () => {
    const due = (rating: 1 | 2 | 3 | 4) =>
      rate(null, rating, at(day0)).due.slice(0, 10);
    // ts-fsrs 6 default parameters, no short-term steps.
    expect(due(Rating.Again)).toBe("2026-11-11");
    expect(due(Rating.Hard)).toBe("2026-11-12");
    expect(due(Rating.Good)).toBe("2026-11-13");
    expect(due(Rating.Easy)).toBe("2026-11-18");
  });

  it("lengthens the interval after each Good", () => {
    let card = rate(null, Rating.Good, at(day0));
    const gaps: number[] = [];
    for (let i = 0; i < 4; i += 1) {
      const when = new Date(card.due);
      const next = rate(card, Rating.Good, when);
      gaps.push(next.scheduled_days);
      card = next;
    }
    expect(gaps).toEqual([...gaps].sort((a, b) => a - b));
    expect(gaps[3]).toBeGreaterThan(gaps[0]);
  });

  it("brings a finished lesson back 3 days later", () => {
    const document = finishLesson(emptyTraining(), "cpp-compile", at(day0));
    const card = cardOf(source(document, lessonKey("cpp-compile")), document);
    expect(card.due.slice(0, 10)).toBe("2026-11-13");
    expect(dueOn("2026-11-12", document, noSolves)).toHaveLength(0);
    expect(dueOn("2026-11-13", document, noSolves)).toHaveLength(1);
  });

  it("survives a round trip through the server's JSON", () => {
    let document = finishLesson(emptyTraining(), "cpp-compile", at(day0));
    document = answerReview(
      document,
      source(document, lessonKey("cpp-compile")),
      Rating.Good,
      at("2026-11-13"),
    );
    const back = normalizeTraining(JSON.parse(JSON.stringify(document)));
    expect(back.cards).toEqual(document.cards);
  });
});

describe("successive relearning", () => {
  const key = itemKey("cpp-template");
  const missed = () =>
    setMissed(emptyTraining(), "cpp-template", true, at(day0));

  it("brings a missed problem back the next day", () => {
    const document = missed();
    expect(dueOn(day0, document, noSolves)).toHaveLength(0);
    expect(dueOn(addDays(day0, 1), document, noSolves)[0].source.key).toBe(key);
  });

  it("counts a missed problem again only after two correct answers on later days", () => {
    let document = missed();
    const answer = (date: string, rating: 1 | 2 | 3 | 4) => {
      document = answerReview(
        document,
        source(document, key),
        rating,
        at(date),
      );
    };

    // Right the same day as the miss: does not count.
    answer(day0, Rating.Good);
    expect(document.cards[key].relearning?.correct).toEqual([]);
    expect(document.items["cpp-template"].missed).toBe(true);

    // Right on day 1: one of two. Due again the next day.
    answer("2026-11-11", Rating.Good);
    expect(document.cards[key].relearning?.correct).toEqual(["2026-11-11"]);
    expect(document.cards[key].due.slice(0, 10)).toBe("2026-11-12");

    // Right twice on one day counts once.
    answer("2026-11-11", Rating.Easy);
    expect(document.cards[key].relearning?.correct).toEqual(["2026-11-11"]);
    expect(document.items["cpp-template"].missed).toBe(true);

    // Right on day 2: relearned. It counts again and leaves relearning.
    answer("2026-11-12", Rating.Good);
    expect(document.cards[key].relearning).toBeUndefined();
    expect(document.items["cpp-template"].missed).toBeUndefined();
    // It stays in review, on FSRS's own interval now.
    expect(reviewSources(document, noSolves).map((s) => s.key)).toContain(key);
    expect(document.cards[key].due > at("2026-11-13").toISOString()).toBe(true);
  });

  it("starts over on a miss during relearning", () => {
    let document = missed();
    document = answerReview(
      document,
      source(document, key),
      Rating.Good,
      at("2026-11-11"),
    );
    document = answerReview(
      document,
      source(document, key),
      Rating.Again,
      at("2026-11-12"),
    );
    expect(document.cards[key].relearning?.correct).toEqual([]);
    expect(document.items["cpp-template"].missed).toBe(true);
  });
});

describe("interleaved review", () => {
  it("deals reviews round robin across units", () => {
    const fake = (unit: string, n: number): Scheduled =>
      ({
        source: { key: `${unit}-${n}`, unit: { id: unit } },
        card: { due: `2026-11-1${n}T00:00:00.000Z` },
        due: `2026-11-1${n}`,
      }) as unknown as Scheduled;
    const order = interleaveReviews([
      fake("a", 1),
      fake("a", 2),
      fake("a", 3),
      fake("b", 4),
      fake("b", 5),
      fake("c", 6),
    ]).map((entry) => entry.source.key);
    expect(order).toEqual(["a-1", "b-4", "c-6", "a-2", "b-5", "a-3"]);
  });
});

describe("the overdue rule", () => {
  /** Lessons finished long ago, so their reviews are overdue. */
  function backlog(lessons: string[]): TrainingDocument {
    let document = emptyTraining();
    for (const id of lessons)
      document = finishLesson(document, id, at("2026-10-01"));
    return document;
  }

  it("puts due reviews first and keeps new material while under a session", () => {
    const document = backlog(["cpp-compile"]);
    const reviews = dueOn(day0, document, noSolves).map(asDue);
    const next = nextUp(day0, document, noSolves, at(day0), { reviews });
    expect(next.entries[0].kind).toBe("review");
    expect(next.overdue).toBe(5);
    expect(next.newWaits).toBe(false);
    expect(next.entries.some((entry) => entry.kind !== "review")).toBe(true);
  });

  it("holds new material back while more than a session is overdue", () => {
    // Thirteen lesson reviews of 5 min is 65 min, over the 60 min session.
    const lessons = syllabus.units
      .filter((unit) => !unit.locked)
      .flatMap((unit) => unit.lessons)
      .slice(0, 13)
      .map((lesson) => lesson.id);
    const document = backlog(lessons);
    const reviews = dueOn(day0, document, noSolves).map(asDue);
    expect(reviews).toHaveLength(13);
    const next = nextUp(day0, document, noSolves, at(day0), { reviews });
    expect(next.overdue).toBe(65);
    expect(next.newWaits).toBe(true);
    expect(next.entries.every((entry) => entry.kind === "review")).toBe(true);
    // Keep going adds more reviews, never new material.
    const more = nextUp(day0, document, noSolves, at(day0), {
      reviews,
      more: 20,
    });
    expect(more.entries).toHaveLength(13);
    expect(more.entries.every((entry) => entry.kind === "review")).toBe(true);

    // A longer session holds the backlog, and new material comes back.
    const longer = nextUp(day0, document, noSolves, at(day0), {
      reviews,
      session: 90,
    });
    expect(longer.newWaits).toBe(false);
  });

  it("counts a review due today as due, not overdue", () => {
    const document = finishLesson(emptyTraining(), "cpp-compile", at(day0));
    const date = "2026-11-13";
    const reviews = dueOn(date, document, noSolves).map(asDue);
    const next = nextUp(date, document, noSolves, at(date), { reviews });
    expect(reviews).toHaveLength(1);
    expect(next.overdue).toBe(0);
  });
});

describe("notes search", () => {
  const now = at(day0);
  let document = setNote(emptyTraining(), "cp-3", "Stars and bars.", now);
  document = setNote(
    document,
    "drill-sum",
    "Use long long for sums.",
    at("2026-11-11"),
  );

  it("finds notes holding every word, accents and case ignored", () => {
    expect(searchNotes(document, "STARS bars").map((hit) => hit.id)).toEqual([
      "cp-3",
    ]);
    expect(searchNotes(document, "stärs")).toHaveLength(1);
    expect(searchNotes(document, "stars long")).toHaveLength(0);
  });

  it("matches the title it is kept under, and links its lesson", () => {
    const [hit] = searchNotes(document, "sum");
    expect(hit.id).toBe("drill-sum");
    expect(hit.lessonId).toBe("cpp-drills");
  });

  it("lists every note newest first when the query is empty", () => {
    expect(searchNotes(document, "").map((hit) => hit.id)).toEqual([
      "drill-sum",
      "cp-3",
    ]);
  });
});
