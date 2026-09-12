import {
  type CurriculumDocument,
  normaliseCurriculum,
} from "../../progress/core/curriculum";
import { useCurriculumProgress } from "../../progress/hooks/useCurriculumProgress";
import type { Result } from "../core/drills";

/**
 * Everything the owner keeps about the plan, in one saved document: the
 * ticked tasks and the attempts at each gate, which every plan keeps, and
 * the drill history, which only this one does.
 */
export interface TrainingDocument extends CurriculumDocument {
  drills: Result[];
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
    ...normaliseCurriculum(value),
    drills: Array.isArray(value?.drills) ? value.drills : [],
  };
}

export function useTrainingProgress() {
  const progress = useCurriculumProgress<TrainingDocument>(
    documentKey,
    normalise,
  );

  return {
    ...progress,
    drills: progress.document.drills,
    recordDrill: (result: Result) =>
      progress.change((current) => ({
        ...current,
        drills: [...current.drills, result].slice(-keepDrills),
      })),
    empty,
  };
}
