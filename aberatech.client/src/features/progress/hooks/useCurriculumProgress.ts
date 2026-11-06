import { useCallback } from "react";
import {
  addAttempt,
  type CurriculumDocument,
  clearDone,
  normalizeCurriculum,
  removeAttempt,
  toggleDone,
} from "../core/curriculum";
import type { Attempt } from "../core/gates";
import { useOwnerDocument } from "./useOwnerDocument";

/**
 * The owner's progress through one plan: the ticks and the gate log, loaded
 * once from the server and saved a beat after each change.
 *
 * A plan that keeps more than that passes its own `normalize`, which must
 * at least do what `normalizeCurriculum` does, and reads the rest from
 * `document`.
 */
export function useCurriculumProgress<
  T extends CurriculumDocument = CurriculumDocument,
>(
  key: string,
  normalize: (value: Partial<T> | null) => T = normalizeCurriculum as (
    value: Partial<T> | null,
  ) => T,
) {
  const { status, value, set, saving } = useOwnerDocument<T>(key);
  const document = normalize(value);
  const change = useCallback(
    (fn: (current: T) => T) => set((current) => fn(normalize(current))),
    [set, normalize],
  );

  return {
    status,
    saving,
    document,
    change,
    done: new Set(document.done),
    toggle: (id: string) => change((current) => toggleDone(current, id)),
    reset: () => change((current) => clearDone(current)),
    gates: document.gates,
    addAttempt: (blockId: string, attempt: Attempt) =>
      change((current) => addAttempt(current, blockId, attempt)),
    removeAttempt: (blockId: string, id: string) =>
      change((current) => removeAttempt(current, blockId, id)),
  };
}
