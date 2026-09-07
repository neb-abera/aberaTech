import { useCallback } from "react";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import type { Result } from "../core/drills";
import type { Attempt } from "../core/gates";

/**
 * Everything the owner keeps about the plan, in one saved document: the
 * ticked tasks, the drill history, and the attempts at each gate.
 */
export interface TrainingDocument {
  version: 1;
  done: string[];
  drills: Result[];
  gates: Record<string, Attempt[]>;
}

export const documentKey = "rf-training";

/** Drill sessions kept; the table never shows more than the last few. */
const keepDrills = 60;

const empty = (): TrainingDocument => ({
  version: 1,
  done: [],
  drills: [],
  gates: {},
});

/** Whatever came back, made whole: a missing or malformed part is empty. */
function normalise(value: Partial<TrainingDocument> | null): TrainingDocument {
  return {
    version: 1,
    done: Array.isArray(value?.done)
      ? value.done.filter((id): id is string => typeof id === "string")
      : [],
    drills: Array.isArray(value?.drills) ? value.drills : [],
    gates:
      value?.gates &&
      typeof value.gates === "object" &&
      !Array.isArray(value.gates)
        ? value.gates
        : {},
  };
}

export function useTrainingProgress() {
  const { status, value, set, saving } =
    useOwnerDocument<TrainingDocument>(documentKey);
  const document = normalise(value);
  const change = useCallback(
    (fn: (current: TrainingDocument) => TrainingDocument) =>
      set((current) => fn(normalise(current))),
    [set],
  );

  return {
    status,
    saving,
    done: new Set(document.done),
    toggle: (id: string) =>
      change((current) => ({
        ...current,
        done: current.done.includes(id)
          ? current.done.filter((other) => other !== id)
          : [...current.done, id],
      })),
    reset: () => change((current) => ({ ...current, done: [] })),
    drills: document.drills,
    recordDrill: (result: Result) =>
      change((current) => ({
        ...current,
        drills: [...current.drills, result].slice(-keepDrills),
      })),
    gates: document.gates,
    addAttempt: (blockId: string, attempt: Attempt) =>
      change((current) => ({
        ...current,
        gates: {
          ...current.gates,
          [blockId]: [...(current.gates[blockId] ?? []), attempt],
        },
      })),
    removeAttempt: (blockId: string, id: string) =>
      change((current) => ({
        ...current,
        gates: {
          ...current.gates,
          [blockId]: (current.gates[blockId] ?? []).filter(
            (attempt) => attempt.id !== id,
          ),
        },
      })),
    empty,
  };
}
