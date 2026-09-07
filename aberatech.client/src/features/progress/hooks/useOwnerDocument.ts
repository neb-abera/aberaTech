import { useCallback, useEffect, useRef, useState } from "react";
import { loadDocument, saveDocument } from "../core/documents";

export type OwnerStatus = "loading" | "visitor" | "owner" | "error";

export interface OwnerDocument<T> {
  status: OwnerStatus;
  /** Null until loaded, and null for a visitor. */
  value: T | null;
  /** Replace the document and schedule a save. Ignored unless the owner. */
  set: (next: T | ((current: T | null) => T)) => void;
  /** A save is scheduled or in flight. */
  saving: boolean;
}

/**
 * One of the owner's documents, loaded once and saved a beat after each
 * change.
 *
 * Loaded in an effect, never during render: the page is prerendered at
 * build time with no server to ask, and hydration must match that HTML, so
 * the first render is always "loading" and read-only. Saves are debounced,
 * and the last pending one is flushed with keepalive when the page hides,
 * so closing the tab a second after a tick does not lose the tick.
 */
export function useOwnerDocument<T>(
  key: string,
  debounceMs = 800,
): OwnerDocument<T> {
  const [status, setStatus] = useState<OwnerStatus>("loading");
  const [value, setValue] = useState<T | null>(null);
  const [saving, setSaving] = useState(false);
  const pending = useRef<T | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const statusRef = useRef<OwnerStatus>("loading");

  useEffect(() => {
    let cancelled = false;
    void loadDocument<T>(key).then((loaded) => {
      if (cancelled) return;
      statusRef.current = loaded.status;
      setStatus(loaded.status);
      if (loaded.status === "owner") setValue(loaded.value);
    });
    return () => {
      cancelled = true;
    };
  }, [key]);

  const flush = useCallback(
    (keepalive: boolean) => {
      if (timer.current !== null) {
        clearTimeout(timer.current);
        timer.current = null;
      }
      const next = pending.current;
      if (next === null) return;
      pending.current = null;
      void saveDocument(key, next, keepalive).then(() => {
        if (pending.current === null) setSaving(false);
      });
    },
    [key],
  );

  useEffect(() => {
    const onHide = () => flush(true);
    window.addEventListener("pagehide", onHide);
    return () => {
      window.removeEventListener("pagehide", onHide);
      flush(true);
    };
  }, [flush]);

  const set = useCallback(
    (next: T | ((current: T | null) => T)) => {
      if (statusRef.current !== "owner") return;
      setValue((current) => {
        const resolved =
          typeof next === "function"
            ? (next as (current: T | null) => T)(current)
            : next;
        pending.current = resolved;
        return resolved;
      });
      setSaving(true);
      if (timer.current !== null) clearTimeout(timer.current);
      timer.current = setTimeout(() => flush(false), debounceMs);
    },
    [debounceMs, flush],
  );

  return { status, value, set, saving };
}
