import { useCallback, useEffect, useState } from "react";
import { readJson, writeJson } from "../core/storage";

/**
 * Which tasks the visitor has ticked, kept in this browser.
 *
 * Read in an effect rather than in the initial state on purpose: the page is
 * rendered to HTML at build time, where there is no localStorage, and the
 * browser then hydrates that markup. Initial state that differed between the
 * two would be a hydration mismatch on every visit with any progress saved.
 * So the first render is always "nothing ticked", and the stored set arrives
 * one effect later.
 */

export const storageKey = "rf-training-progress";

const asIds = (value: unknown): string[] | null =>
  Array.isArray(value)
    ? value.filter((id): id is string => typeof id === "string")
    : null;

export function useProgress(): {
  done: Set<string>;
  toggle: (id: string) => void;
  reset: () => void;
} {
  const [done, setDone] = useState<Set<string>>(() => new Set());

  useEffect(() => {
    setDone(new Set(readJson(storageKey, asIds) ?? []));
  }, []);

  const toggle = useCallback((id: string) => {
    setDone((previous) => {
      const next = new Set(previous);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      writeJson(storageKey, [...next]);
      return next;
    });
  }, []);

  const reset = useCallback(() => {
    writeJson(storageKey, []);
    setDone(new Set());
  }, []);

  return { done, toggle, reset };
}
