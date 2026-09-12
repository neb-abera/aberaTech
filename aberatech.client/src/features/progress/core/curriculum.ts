/**
 * A curriculum as data, and the owner's progress through one.
 *
 * Two of the site's guides are plans: blocks of tasks, each block ending in
 * a gate with a clock or a count on it, each block listing reading and
 * practice that scores you. The shapes and the operations on the saved
 * document live here once, so the field radio plan and the signal
 * processing plan render and save the same way, and a fix to one is a fix
 * to both. A plan's own file holds only its content.
 */

import { type Attempt, asAttemptLog } from "./gates";

export interface Resource {
  title: string;
  url: string;
  /** What to do with it, when the title alone does not say. */
  note?: string;
}

export interface Task {
  /** Stable, unique across the whole plan: it is the key progress is stored under. */
  id: string;
  text: string;
}

export interface Block {
  id: string;
  title: string;
  /** When in the plan this block runs. */
  weeks: string;
  /** Why the block exists, in one or two sentences. */
  why: string;
  tasks: Task[];
  /** A pass or fail test with a clock or a count on it. The block is done when this is. */
  gate: string;
  /** Reading and reference. */
  resources: Resource[];
  /**
   * Things that score you: problem sets, drills, graded courses. Their
   * results go in the gate log's note.
   */
  practice: Resource[];
}

/** One line of a plan's weekly cadence: what, and how much. */
export interface CadenceItem {
  label: string;
  detail: string;
}

/** Every task in a plan, in the order the page shows them. */
export const tasksOf = (plan: Block[]): Task[] =>
  plan.flatMap((block) => block.tasks);

/**
 * What the owner keeps about a plan: the ticked tasks and the attempts at
 * each gate. A plan that keeps more (the radio plan keeps its drill history)
 * extends this and normalises the rest itself.
 */
export interface CurriculumDocument {
  version: 1;
  done: string[];
  gates: Record<string, Attempt[]>;
}

export const emptyCurriculum = (): CurriculumDocument => ({
  version: 1,
  done: [],
  gates: {},
});

/** Whatever came back, made whole: a missing or malformed part is empty. */
export function normaliseCurriculum(
  value: Partial<CurriculumDocument> | null,
): CurriculumDocument {
  return {
    version: 1,
    done: Array.isArray(value?.done)
      ? value.done.filter((id): id is string => typeof id === "string")
      : [],
    gates: asAttemptLog(value?.gates) ?? {},
  };
}

export function toggleDone<T extends CurriculumDocument>(
  document: T,
  id: string,
): T {
  return {
    ...document,
    done: document.done.includes(id)
      ? document.done.filter((other) => other !== id)
      : [...document.done, id],
  };
}

export function clearDone<T extends CurriculumDocument>(document: T): T {
  return { ...document, done: [] };
}

export function addAttempt<T extends CurriculumDocument>(
  document: T,
  blockId: string,
  attempt: Attempt,
): T {
  return {
    ...document,
    gates: {
      ...document.gates,
      [blockId]: [...(document.gates[blockId] ?? []), attempt],
    },
  };
}

export function removeAttempt<T extends CurriculumDocument>(
  document: T,
  blockId: string,
  id: string,
): T {
  return {
    ...document,
    gates: {
      ...document.gates,
      [blockId]: (document.gates[blockId] ?? []).filter(
        (attempt) => attempt.id !== id,
      ),
    },
  };
}
