import { useCallback, useMemo } from "react";
import { accountSignedIn } from "../../../hooks/useAccount";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import {
  normalizeTraining,
  type TrainingDocument,
  trainingDocumentKey,
} from "../core/progress";

/**
 * The owner's training document: loaded once the account answers, saved a
 * beat after each change. A visitor's browser asks only whether anyone is
 * signed in, and never for the document.
 */
export function useTraining() {
  const owner = useOwnerDocument<TrainingDocument>(
    trainingDocumentKey,
    800,
    accountSignedIn,
  );
  const { set } = owner;
  const document = useMemo(() => normalizeTraining(owner.value), [owner.value]);
  const change = useCallback(
    (fn: (current: TrainingDocument) => TrainingDocument) =>
      set((current: TrainingDocument | null) => fn(normalizeTraining(current))),
    [set],
  );
  return {
    status: owner.status,
    saving: owner.saving,
    failed: owner.failed,
    document,
    change,
  };
}

export type Training = ReturnType<typeof useTraining>;
