/**
 * The review queue: what comes back, and when.
 *
 * Three things come back. A problem marked missed, a lesson once every
 * item in it is done (or its unit's mastery check is passed), and a
 * question: every card, and any other question answered wrong. Each is a
 * review key: "item:<id>", "lesson:<id>", or "q:<id>".
 * A key's first due date comes from the event itself, a miss rated Again
 * and a finished lesson rated Good, so nothing is stored until the first
 * review. From then on the key keeps its FSRS card in the document's
 * `cards`, and it stays in the queue at growing intervals.
 *
 * FSRS is the scheduler Anki ships. ts-fsrs (MIT) runs it here with its
 * default parameters, 90% target retention, no fuzz, and whole days: no
 * minute steps, since a session is a day's work.
 *
 * Successive relearning (Rawson and Dunlosky): a miss starts relearning.
 * The key is due again the next day, every day, until it has been answered
 * right on two later days. Only then does a missed problem count again,
 * and FSRS's longer intervals take over.
 */
import {
  type Card,
  createEmptyCard,
  fsrs,
  type Grade,
  generatorParameters,
  Rating,
} from "ts-fsrs";
import {
  latestAnswers,
  masteryState,
  questionMinutes,
  questionsById,
} from "./assessment";
import { localDate, localDateOf } from "./dates";
import {
  itemStatus,
  type ReviewCard,
  type Solves,
  type TrainingDocument,
} from "./progress";
import {
  type Item,
  type Lesson,
  locate,
  type Question,
  type Syllabus,
  syllabus,
  type Track,
  type Unit,
} from "./syllabus";
import type { DueReview } from "./today";

export type { Grade };
export { Rating };

/** The parameters, stated where the page shows its source. */
export const scheduler = fsrs(
  generatorParameters({
    enable_fuzz: false,
    enable_short_term: false,
    learning_steps: [],
    relearning_steps: [],
  }),
);

export const schedulerSource =
  "Scheduled by FSRS (ts-fsrs 6, default parameters, 90% target retention).";

/** Correct answers on later days before a miss counts again. */
export const relearnDays = 2;

/** Minutes a review takes. Estimates, and the page says so. */
export const reviewMinutes = {
  lesson: 5,
  /** A problem again: its own minutes, at most this. */
  itemCap: 15,
};

export const reviewMinutesBasis =
  "A lesson review is 5 min. A problem review is the problem's minutes, at most 15. Both are estimates.";

export type ReviewKind = "lesson" | "item" | "question";

export interface ReviewSource {
  key: string;
  kind: ReviewKind;
  /** When it entered review, an ISO date and time. */
  at: string;
  /** How the entering event rates: a miss is Again, a finished lesson Good. */
  rating: Grade;
  unit: Unit;
  lesson?: Lesson;
  item?: Item;
  question?: Question;
  title: string;
  track: Track;
  minutes: number;
}

export const questionKey = (id: string): string => `q:${id}`;

function questionSource(
  question: Question,
  lesson: Lesson,
  unit: Unit,
  at: string,
  rating: Grade,
): ReviewSource {
  return {
    key: questionKey(question.id),
    kind: "question",
    at,
    rating,
    unit,
    lesson,
    question,
    title: question.prompt,
    track: unit.track,
    minutes: questionMinutes(question),
  };
}

export const lessonKey = (id: string): string => `lesson:${id}`;
export const itemKey = (id: string): string => `item:${id}`;

function itemSource(
  item: Item,
  unit: Unit,
  lesson: Lesson | undefined,
  at: string,
  rating: Grade,
): ReviewSource {
  return {
    key: itemKey(item.id),
    kind: "item",
    at,
    rating,
    unit,
    lesson,
    item,
    title: item.title,
    track: unit.track,
    minutes: Math.min(item.minutes, reviewMinutes.itemCap),
  };
}

function lessonSource(
  lesson: Lesson,
  unit: Unit,
  at: string,
  rating: Grade,
): ReviewSource {
  return {
    key: lessonKey(lesson.id),
    kind: "lesson",
    at,
    rating,
    unit,
    lesson,
    title: lesson.title,
    track: unit.track,
    minutes: reviewMinutes.lesson,
  };
}

/** The time a lesson was finished: its last item's. Null while open. */
export function lessonFinished(
  lesson: Lesson,
  document: TrainingDocument,
  solves: Solves,
): string | null {
  if (lesson.items.length === 0) return null;
  let latest = "";
  for (const item of lesson.items) {
    const status = itemStatus(item, document, solves);
    if (!status.done || !status.at) return null;
    if (status.at > latest) latest = status.at;
  }
  return latest;
}

/**
 * Everything in review: every missed problem, every finished lesson, and
 * every key that has a card, whatever put it there.
 */
export function reviewSources(
  document: TrainingDocument,
  solves: Solves,
  from: Syllabus = syllabus,
): ReviewSource[] {
  const found = new Map<string, ReviewSource>();
  for (const unit of from.units) {
    if (unit.locked) continue;
    // A mastery check passed closes the unit, and its lessons enter review.
    const mastery = masteryState(unit, document, from);
    const mastered = mastery.state === "passed" ? mastery.at : null;
    for (const lesson of unit.lessons) {
      const finished = lessonFinished(lesson, document, solves) ?? mastered;
      if (finished)
        found.set(
          lessonKey(lesson.id),
          lessonSource(lesson, unit, finished, Rating.Good),
        );
      for (const item of lesson.items) {
        const record = document.items[item.id];
        if (record?.missed)
          found.set(
            itemKey(item.id),
            itemSource(item, unit, lesson, record.at, Rating.Again),
          );
      }
    }
    for (const item of unit.checkpoint?.items ?? []) {
      const record = document.items[item.id];
      if (record?.missed)
        found.set(
          itemKey(item.id),
          itemSource(item, unit, undefined, record.at, Rating.Again),
        );
    }
  }
  const questions = questionsById(from);
  for (const [id, { answer, at }] of latestAnswers(document)) {
    const place = questions.get(id);
    if (!place || place.unit.locked) continue;
    const rating: Grade | null =
      place.question.type === "card"
        ? ((answer.rating ?? (answer.correct ? 3 : 1)) as Grade)
        : answer.correct
          ? null
          : Rating.Again;
    if (rating === null) continue;
    found.set(
      questionKey(id),
      questionSource(place.question, place.lesson, place.unit, at, rating),
    );
  }
  for (const key of Object.keys(document.cards)) {
    if (found.has(key)) continue;
    const source = sourceOfKey(key, document, from);
    if (source) found.set(key, source);
  }
  return [...found.values()];
}

/** A key with a card whose event is gone, such as a miss since relearned. */
function sourceOfKey(
  key: string,
  document: TrainingDocument,
  from: Syllabus,
): ReviewSource | null {
  const [kind, id] = splitKey(key);
  const card = document.cards[key];
  const at = card.last_review ?? card.due;
  if (kind === "q") {
    const found = questionsById(from).get(id);
    return found
      ? questionSource(
          found.question,
          found.lesson,
          found.unit,
          at,
          Rating.Good,
        )
      : null;
  }
  const place = id ? locate(id, from) : null;
  if (!place) return null;
  if (kind === "lesson" && place.lesson && !place.item)
    return lessonSource(place.lesson, place.unit, at, Rating.Good);
  if (kind === "item" && place.item)
    return itemSource(place.item, place.unit, place.lesson, at, Rating.Good);
  return null;
}

export function splitKey(key: string): [string, string] {
  const at = key.indexOf(":");
  return at < 0 ? [key, ""] : [key.slice(0, at), key.slice(at + 1)];
}

// ---------------------------------------------------------------- cards

export const toCard = (card: ReviewCard): Card => ({
  due: new Date(card.due),
  stability: card.stability,
  difficulty: card.difficulty,
  scheduled_days: card.scheduled_days,
  learning_steps: card.learning_steps,
  reps: card.reps,
  lapses: card.lapses,
  state: card.state,
  last_review: card.last_review ? new Date(card.last_review) : undefined,
});

const fromCard = (card: Card): ReviewCard => {
  const out: ReviewCard = {
    due: card.due.toISOString(),
    stability: card.stability,
    difficulty: card.difficulty,
    scheduled_days: card.scheduled_days,
    learning_steps: card.learning_steps,
    reps: card.reps,
    lapses: card.lapses,
    state: card.state as ReviewCard["state"],
  };
  if (card.last_review) out.last_review = card.last_review.toISOString();
  return out;
};

const nextDay = (moment: Date): Date => new Date(moment.getTime() + 86_400_000);

/**
 * Apply one answer to a card. A miss starts relearning. A correct answer
 * on a later day than the miss, and on a day not yet counted, counts
 * toward the two. While relearning the card is due again the next day.
 */
export function rate(
  current: ReviewCard | null,
  rating: Grade,
  now: Date,
): ReviewCard {
  const base = current ? toCard(current) : createEmptyCard(now);
  const next = fromCard(scheduler.next(base, now, rating).card);
  let relearning = current?.relearning;
  if (rating === Rating.Again)
    relearning = { since: now.toISOString(), correct: [] };
  else if (relearning) {
    const today = localDate(now);
    const missedOn = localDateOf(relearning.since);
    const correct =
      today > missedOn && !relearning.correct.includes(today)
        ? [...relearning.correct, today]
        : relearning.correct;
    relearning =
      correct.length >= relearnDays ? undefined : { ...relearning, correct };
  }
  if (relearning) {
    next.relearning = relearning;
    const cap = nextDay(now).toISOString();
    if (next.due > cap) next.due = cap;
  }
  return next;
}

/** A key's card now: the stored one, or the one its event implies. */
export function cardOf(
  source: ReviewSource,
  document: TrainingDocument,
): ReviewCard {
  const stored = document.cards[source.key];
  if (!stored) return rate(null, source.rating, new Date(source.at));
  // Missed again after it was relearned: a new miss, from its last review.
  // A problem's missed mark is cleared when it is relearned, so a mark
  // there is a new miss. A question's miss is new when it is later than
  // the card's last review.
  const newMiss =
    source.kind === "question" ? source.at > (stored.last_review ?? "") : true;
  if (source.rating === Rating.Again && !stored.relearning && newMiss) {
    const at = [source.at, stored.last_review ?? source.at].sort()[1];
    return rate(stored, Rating.Again, new Date(at));
  }
  return stored;
}

/** Still relearning: a miss not yet answered right on two later days. */
export const relearning = (card: ReviewCard): boolean => !!card.relearning;

/**
 * Record an answer to a review key. When a missed problem's relearning
 * ends, its missed mark is cleared: it counts again.
 */
export function answerReview(
  document: TrainingDocument,
  source: ReviewSource,
  rating: Grade,
  now: Date,
): TrainingDocument {
  const before = cardOf(source, document);
  const after = rate(before, rating, now);
  const next: TrainingDocument = {
    ...document,
    cards: { ...document.cards, [source.key]: after },
    reviews: { ...document.reviews, [source.key]: now.toISOString() },
  };
  if (
    source.kind === "item" &&
    source.item &&
    before.relearning &&
    !after.relearning
  ) {
    const record = next.items[source.item.id];
    if (record?.missed) {
      const { missed: _, ...rest } = record;
      next.items = { ...next.items, [source.item.id]: rest };
    }
  }
  return next;
}

/** The interval each answer would give, in days, for the buttons. */
export function preview(
  source: ReviewSource,
  document: TrainingDocument,
  now: Date,
): Record<Grade, number> {
  const card = cardOf(source, document);
  const out = {} as Record<Grade, number>;
  for (const rating of [
    Rating.Again,
    Rating.Hard,
    Rating.Good,
    Rating.Easy,
  ] as Grade[]) {
    const after = rate(card, rating, now);
    out[rating] = Math.max(
      1,
      Math.round((Date.parse(after.due) - now.getTime()) / 86_400_000),
    );
  }
  return out;
}

// ---------------------------------------------------------------- the queue

export interface Scheduled {
  source: ReviewSource;
  card: ReviewCard;
  /** The local date it is due. */
  due: string;
}

export function scheduled(
  document: TrainingDocument,
  solves: Solves,
  from: Syllabus = syllabus,
): Scheduled[] {
  return reviewSources(document, solves, from).map((source) => {
    const card = cardOf(source, document);
    return { source, card, due: localDateOf(card.due) };
  });
}

/**
 * Mixed practice: due reviews dealt round robin across units, the unit
 * with the oldest due review first, so no two reviews in a row come from
 * one unit while another has some waiting.
 */
export function interleaveReviews(due: Scheduled[]): Scheduled[] {
  const groups = new Map<string, Scheduled[]>();
  const sorted = [...due].sort(
    (a, b) =>
      a.card.due.localeCompare(b.card.due) ||
      a.source.key.localeCompare(b.source.key),
  );
  for (const entry of sorted) {
    const group = groups.get(entry.source.unit.id) ?? [];
    group.push(entry);
    groups.set(entry.source.unit.id, group);
  }
  const queues = [...groups.values()];
  const out: Scheduled[] = [];
  for (let round = 0; out.length < sorted.length; round += 1)
    for (const queue of queues)
      if (round < queue.length) out.push(queue[round]);
  return out;
}

/** Reviews due on or before `date`, mixed across units. */
export function dueOn(
  date: string,
  document: TrainingDocument,
  solves: Solves,
  from: Syllabus = syllabus,
): Scheduled[] {
  return interleaveReviews(
    scheduled(document, solves, from).filter((entry) => entry.due <= date),
  );
}

/** As Next up takes them. */
export function asDue(entry: Scheduled): DueReview {
  return {
    key: entry.source.key,
    track: entry.source.track,
    title: entry.source.title,
    minutes: entry.source.minutes,
    unit: entry.source.unit,
    lesson: entry.source.lesson,
    item: entry.source.item,
    due: entry.due,
    from: localDateOf(entry.source.at),
  };
}

/** How many come due on each of the next `days` days after `date`. */
export function upcoming(
  date: string,
  document: TrainingDocument,
  solves: Solves,
  days: number,
  from: Syllabus = syllabus,
): number {
  const all = scheduled(document, solves, from);
  const end = new Date(`${date}T12:00:00Z`);
  end.setUTCDate(end.getUTCDate() + days);
  const last = end.toISOString().slice(0, 10);
  return all.filter((entry) => entry.due > date && entry.due <= last).length;
}
