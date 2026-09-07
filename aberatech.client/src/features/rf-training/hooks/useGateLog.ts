import { useCallback, useEffect, useState } from "react";
import { type Attempt, asAttemptLog } from "../core/gates";
import { readJson, writeJson } from "../core/storage";

/** Attempts at each block's gate, by block id, kept in this browser. */

export const storageKey = "rf-training-gates";

export function useGateLog(): {
  log: Record<string, Attempt[]>;
  add: (blockId: string, attempt: Attempt) => void;
  remove: (blockId: string, id: string) => void;
} {
  const [log, setLog] = useState<Record<string, Attempt[]>>({});

  useEffect(() => {
    setLog(readJson(storageKey, asAttemptLog) ?? {});
  }, []);

  const add = useCallback((blockId: string, attempt: Attempt) => {
    setLog((previous) => {
      const next = {
        ...previous,
        [blockId]: [...(previous[blockId] ?? []), attempt],
      };
      writeJson(storageKey, next);
      return next;
    });
  }, []);

  const remove = useCallback((blockId: string, id: string) => {
    setLog((previous) => {
      const entries = (previous[blockId] ?? []).filter(
        (attempt) => attempt.id !== id,
      );
      const next = { ...previous, [blockId]: entries };
      writeJson(storageKey, next);
      return next;
    });
  }, []);

  return { log, add, remove };
}
