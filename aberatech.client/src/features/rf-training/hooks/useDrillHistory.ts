import { useCallback, useEffect, useState } from "react";
import type { Result } from "../core/drills";
import { readJson, writeJson } from "../core/storage";

/**
 * Past drill sessions, newest last, kept in this browser. Read in an effect
 * for the same reason useProgress is: the page is prerendered without a
 * window, and the first render must match that HTML.
 */

export const storageKey = "rf-training-drills";

/** Only what the last thirty sessions need; the chart never shows more. */
const keep = 30;

const asResults = (value: unknown): Result[] | null =>
  Array.isArray(value)
    ? value.filter(
        (entry): entry is Result =>
          typeof entry === "object" &&
          entry !== null &&
          typeof (entry as Result).at === "string" &&
          typeof (entry as Result).correct === "number" &&
          typeof (entry as Result).total === "number" &&
          typeof (entry as Result).seconds === "number",
      )
    : null;

export function useDrillHistory(): {
  history: Result[];
  record: (result: Result) => void;
} {
  const [history, setHistory] = useState<Result[]>([]);

  useEffect(() => {
    setHistory(readJson(storageKey, asResults) ?? []);
  }, []);

  const record = useCallback((result: Result) => {
    setHistory((previous) => {
      const next = [...previous, result].slice(-keep);
      writeJson(storageKey, next);
      return next;
    });
  }, []);

  return { history, record };
}
