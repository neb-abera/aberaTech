/**
 * Assessment: the questions a lesson ends with, a unit's pretest, and its
 * mastery check, and the rules that read their results.
 *
 * - A lesson with questions ends in a quiz. The lesson is done once every
 *   item is done and the quiz has been taken, right or wrong: nothing is
 *   learned by reading alone. Questions a unit lists in its pretest or its
 *   mastery check are kept out of the lesson's quiz.
 * - A pretest at 90% or more places the unit out, as a checkpoint does. So
 *   does an outside placement test (an AoPS diagnostic) scored at its pass
 *   mark or above.
 * - A mastery check at 90% or more closes the unit. So does an outside
 *   mastery check ticked by hand (named Alcumus topics). Below 90%, the
 *   check opens again 2 days after the attempt.
 *
 * Grading: a choice is right at its index. A number or expression is
 * right when it equals the key by value (grade.ts). A proof is graded by
 * its writer against its rubric, the MIT 18.A34 way: it is right when every
 * correctness line is met. Elegance and clarity are kept beside it, since
 * 18.A34 grants or deducts for them beyond correctness. A card is right
 * when recalled at Hard or better. Code is right when every test passes.
 */
import { addDays, localDateOf } from "./dates";
import { sameValue } from "./grade";
import type { TrainingDocument } from "./progress";
import {
  type ExternalCheck,
  type Lesson,
  type Question,
  type Syllabus,
  syllabus,
  type Unit,
} from "./syllabus";

/** The share of a check that passes it. */
export const passMark = 0.9;

/** Days after a failed mastery check before it opens again. */
export const retestDays = 2;

export interface ProofGrade {
  /** One per rubric line: met or not. */
  met: boolean[];
  elegant: boolean;
  clear: boolean;
}

export interface Answer {
  correct: boolean;
  /** What was given: a choice's index, a number, a proof, code. */
  given?: string;
  proof?: ProofGrade;
  /** A card's recall, 1 Again to 4 Easy. */
  rating?: 1 | 2 | 3 | 4;
}

export interface Attempt {
  /** When it was finished, an ISO date and time. */
  at: string;
  answers: Record<string, Answer>;
}

/** Recorded by hand: an outside check's date, and a placement's score. */
export interface ExternalRecord {
  at: string;
  score?: number;
}

export type CheckKind = "quiz" | "pretest" | "mastery";

/** "quiz:<lesson id>", "pretest:<unit id>", "mastery:<unit id>". */
export const checkKey = (kind: CheckKind, id: string): string =>
  `${kind}:${id}`;

/** The document's assessment parts. */
export interface AssessmentParts {
  assessments: Record<string, Attempt[]>;
  /** "placement:<unit id>" and "mastery:<unit id>". */
  external: Record<string, ExternalRecord>;
}

// ---------------------------------------------------------------- questions

export function questionsById(
  from: Syllabus = syllabus,
): Map<string, { question: Question; lesson: Lesson; unit: Unit }> {
  const out = new Map<
    string,
    { question: Question; lesson: Lesson; unit: Unit }
  >();
  for (const unit of from.units)
    for (const lesson of unit.lessons)
      for (const question of lesson.questions ?? [])
        out.set(question.id, { question, lesson, unit });
  return out;
}

function unitOfLesson(lesson: Lesson, from: Syllabus): Unit | undefined {
  return from.units.find((unit) => unit.lessons.includes(lesson));
}

/** A lesson's quiz: its questions less any its unit holds for a check. */
export function quizQuestions(
  lesson: Lesson,
  from: Syllabus = syllabus,
): Question[] {
  const unit = unitOfLesson(lesson, from);
  const held = new Set([...(unit?.pretest ?? []), ...(unit?.mastery ?? [])]);
  return (lesson.questions ?? []).filter((question) => !held.has(question.id));
}

/** The questions of a check, in the order the unit lists them. */
export function checkQuestions(
  kind: CheckKind,
  id: string,
  from: Syllabus = syllabus,
): Question[] {
  if (kind === "quiz") {
    const lesson = from.units
      .flatMap((unit) => unit.lessons)
      .find((each) => each.id === id);
    return lesson ? quizQuestions(lesson, from) : [];
  }
  const unit = from.units.find((each) => each.id === id);
  const ids = (kind === "pretest" ? unit?.pretest : unit?.mastery) ?? [];
  const all = questionsById(from);
  return ids.flatMap((qid) => {
    const found = all.get(qid);
    return found ? [found.question] : [];
  });
}

/** Minutes a question takes. Estimates, and the page says so. */
export function questionMinutes(question: Question): number {
  switch (question.type) {
    case "proof":
      return 20;
    case "code":
      return 15;
    case "card":
      return 1;
    default:
      return 2;
  }
}

export const questionMinutesBasis =
  "A card is 1 min, a choice or short answer 2, code 15, and a proof 20. All are estimates.";

// ---------------------------------------------------------------- grading

/** Grade an answer the page can grade. Proofs and cards are graded by you. */
export function gradeChoice(
  question: Question & { type: "choice" },
  index: number,
): Answer {
  return { correct: index === question.answer, given: String(index) };
}

export function gradeNumber(
  question: Question & { type: "number" },
  given: string,
): Answer {
  return { correct: sameValue(given, question.answer), given };
}

export function gradeProof(text: string, proof: ProofGrade): Answer {
  return {
    correct: proof.met.length > 0 && proof.met.every(Boolean),
    given: text,
    proof,
  };
}

export function gradeCard(rating: 1 | 2 | 3 | 4): Answer {
  return { correct: rating >= 2, rating };
}

export function gradeCode(source: string, allPassed: boolean): Answer {
  return { correct: allPassed, given: source };
}

/** The right answer, as the page shows it after a wrong one. */
export function rightAnswer(question: Question): string {
  switch (question.type) {
    case "choice":
      return question.choices[question.answer] ?? "";
    case "number":
      return question.answer;
    case "card":
      return question.back;
    case "proof":
      return question.rubric.join(" ");
    case "code":
      return "Every test passing.";
  }
}

/** A reference's address, with a video's time where it names one. */
export function referenceUrl(ref: { url: string; at?: number }): string {
  if (ref.at === undefined) return ref.url;
  try {
    const url = new URL(ref.url);
    if (/(^|\.)youtube(-nocookie)?\.com$|^youtu\.be$/.test(url.hostname)) {
      url.searchParams.set("t", String(Math.floor(ref.at)));
      return url.toString();
    }
  } catch {
    return ref.url;
  }
  return ref.url;
}

/** "at 4:05", for a reference that is not a YouTube link. */
export const timeLabel = (seconds: number): string =>
  `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}`;

// ---------------------------------------------------------------- attempts

export interface Score {
  right: number;
  total: number;
  share: number;
  passed: boolean;
}

export function score(attempt: Attempt, questions: Question[]): Score {
  const total = questions.length;
  const right = questions.filter(
    (question) => attempt.answers[question.id]?.correct,
  ).length;
  const share = total === 0 ? 0 : right / total;
  return { right, total, share, passed: total > 0 && share >= passMark };
}

export function recordAttempt(
  document: TrainingDocument,
  key: string,
  attempt: Attempt,
): TrainingDocument {
  return {
    ...document,
    assessments: {
      ...document.assessments,
      [key]: [...(document.assessments[key] ?? []), attempt],
    },
  };
}

export function setExternal(
  document: TrainingDocument,
  key: string,
  record: ExternalRecord | null,
): TrainingDocument {
  const external = { ...document.external };
  if (record) external[key] = record;
  else delete external[key];
  return { ...document, external };
}

const last = <T>(list: T[] | undefined): T | undefined =>
  list && list.length > 0 ? list[list.length - 1] : undefined;

/** The quiz was taken, so the lesson can close. True when it has none. */
export function quizTaken(
  lesson: Lesson,
  document: TrainingDocument,
  from: Syllabus = syllabus,
): boolean {
  if (quizQuestions(lesson, from).length === 0) return true;
  return (document.assessments[checkKey("quiz", lesson.id)] ?? []).length > 0;
}

export type CheckState =
  | { state: "none" }
  | { state: "passed"; at: string; by: "page" | "outside"; score?: Score }
  | { state: "failed"; at: string; score: Score; opens: string };

function externalPassed(
  check: ExternalCheck | undefined,
  record: ExternalRecord | undefined,
  scored: boolean,
): boolean {
  if (!check || !record) return false;
  if (!scored) return true;
  return (
    record.score !== undefined &&
    check.pass !== undefined &&
    record.score >= check.pass
  );
}

/**
 * A unit's placement: its pretest, or its outside placement test. Passing
 * either places the unit out.
 */
export function placementState(
  unit: Unit,
  document: TrainingDocument,
  from: Syllabus = syllabus,
): CheckState {
  const outside = document.external[`placement:${unit.id}`];
  if (unit.placement && outside) {
    if (externalPassed(unit.placement, outside, true))
      return { state: "passed", at: outside.at, by: "outside" };
  }
  const attempt = last(document.assessments[checkKey("pretest", unit.id)]);
  if (attempt) {
    const result = score(attempt, checkQuestions("pretest", unit.id, from));
    if (result.passed)
      return { state: "passed", at: attempt.at, by: "page", score: result };
    return {
      state: "failed",
      at: attempt.at,
      score: result,
      opens: localDateOf(attempt.at),
    };
  }
  if (outside)
    return {
      state: "failed",
      at: outside.at,
      score: {
        right: outside.score ?? 0,
        total: unit.placement?.outOf ?? 0,
        share: 0,
        passed: false,
      },
      opens: localDateOf(outside.at),
    };
  return { state: "none" };
}

/** Whether the unit has a placement test of either kind. */
export const hasPlacement = (unit: Unit): boolean =>
  (unit.pretest?.length ?? 0) > 0 || !!unit.placement;

/** Whether the unit has a mastery check of either kind. */
export const hasMastery = (unit: Unit): boolean =>
  (unit.mastery?.length ?? 0) > 0 || !!unit.externalMastery;

/**
 * A unit's mastery: its check on the page, or its outside check ticked.
 * A failed check opens again `retestDays` after it, by local date.
 */
export function masteryState(
  unit: Unit,
  document: TrainingDocument,
  from: Syllabus = syllabus,
): CheckState {
  const outside = document.external[`mastery:${unit.id}`];
  if (unit.externalMastery && outside)
    return { state: "passed", at: outside.at, by: "outside" };
  const attempts = document.assessments[checkKey("mastery", unit.id)] ?? [];
  const questions = checkQuestions("mastery", unit.id, from);
  for (const attempt of [...attempts].reverse()) {
    const result = score(attempt, questions);
    if (result.passed)
      return { state: "passed", at: attempt.at, by: "page", score: result };
  }
  const latest = last(attempts);
  if (latest)
    return {
      state: "failed",
      at: latest.at,
      score: score(latest, questions),
      opens: addDays(localDateOf(latest.at), retestDays),
    };
  return { state: "none" };
}

/** The mastery check can be taken on `date`: never taken, or its wait is over. */
export function masteryOpen(
  unit: Unit,
  document: TrainingDocument,
  date: string,
  from: Syllabus = syllabus,
): boolean {
  if ((unit.mastery?.length ?? 0) === 0) return false;
  const state = masteryState(unit, document, from);
  return (
    state.state === "none" || (state.state === "failed" && state.opens <= date)
  );
}

// ---------------------------------------------------------------- reviews

/** The latest answer to each question, from every attempt of every check. */
export function latestAnswers(
  document: TrainingDocument,
): Map<string, { answer: Answer; at: string }> {
  const out = new Map<string, { answer: Answer; at: string }>();
  for (const attempts of Object.values(document.assessments))
    for (const attempt of attempts)
      for (const [id, answer] of Object.entries(attempt.answers)) {
        const seen = out.get(id);
        if (!seen || seen.at <= attempt.at)
          out.set(id, { answer, at: attempt.at });
      }
  return out;
}

// ---------------------------------------------------------------- Anki

const csvField = (text: string): string => `"${text.replace(/"/g, '""')}"`;

const tag = (text: string): string =>
  text
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .replace(/[^A-Za-z0-9]+/g, "_")
    .replace(/^_|_$/g, "");

/**
 * Every card in the syllabus as a file Anki imports (File, Import): comma
 * separated, a header naming the separator, the Basic note type, the deck,
 * and the tags column, then front, back, and tags on each line.
 */
export function ankiCsv(from: Syllabus = syllabus): {
  text: string;
  cards: number;
} {
  const lines = [
    "#separator:Comma",
    "#html:false",
    "#notetype:Basic",
    "#deck:abera.tech training",
    "#tags column:3",
  ];
  let cards = 0;
  for (const unit of from.units)
    for (const lesson of unit.lessons)
      for (const question of lesson.questions ?? []) {
        if (question.type !== "card") continue;
        cards += 1;
        lines.push(
          [
            csvField(question.prompt),
            csvField(question.back),
            csvField(`${tag(unit.id)} ${tag(lesson.id)}`),
          ].join(","),
        );
      }
  return { text: `${lines.join("\n")}\n`, cards };
}
