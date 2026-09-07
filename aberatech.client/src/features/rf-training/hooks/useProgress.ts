import { useCallback, useEffect, useState } from "react";

/**
 * Which tasks the visitor has ticked, kept in this browser.
 *
 * Read in an effect rather than in the initial state on purpose: the page is
 * rendered to HTML at build time, where there is no localStorage, and the
 * browser then hydrates that markup. Initial state that differed between the
 * two would be a hydration mismatch on every visit with any progress saved.
 * So the first render is always "nothing ticked", and the stored set arrives
 * one effect later.
 *
 * Every read and write is guarded: storage can be absent, full, or throw in
 * a private window, and none of those should cost the visitor the page.
 */

export const storageKey = "rf-training-progress";

function read(): Set<string> {
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) return new Set();
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return new Set();
    return new Set(parsed.filter((id): id is string => typeof id === "string"));
  } catch {
    return new Set();
  }
}

function write(done: Set<string>): void {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify([...done]));
  } catch {
    // Storage full or unavailable: the tick still shows for this visit.
  }
}

export function useProgress(): {
  done: Set<string>;
  toggle: (id: string) => void;
  reset: () => void;
} {
  const [done, setDone] = useState<Set<string>>(() => new Set());

  useEffect(() => {
    setDone(read());
  }, []);

  const toggle = useCallback((id: string) => {
    setDone((previous) => {
      const next = new Set(previous);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      write(next);
      return next;
    });
  }, []);

  const reset = useCallback(() => {
    const empty = new Set<string>();
    write(empty);
    setDone(empty);
  }, []);

  return { done, toggle, reset };
}
