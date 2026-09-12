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
 * change, and loaded again whenever the page comes back into view.
 *
 * Loaded in an effect, never during render: the page is prerendered at
 * build time with no server to ask, and hydration must match that HTML, so
 * the first render is always "loading" and read-only. Saves are debounced,
 * and the last pending one is flushed with keepalive when the page hides,
 * so closing the tab a second after a tick does not lose the tick.
 *
 * The reload on return is what keeps two devices honest. A save replaces
 * the whole document, so a tab left open on the laptop while the phone
 * ticks a task would, on its next tick, put the laptop's stale copy back
 * over the phone's. Reloading when the tab is shown again means the copy a
 * tick is applied to is the one the server has. A reload never lands over
 * a tick made while it was in flight: the local change wins and is saved.
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
  /** Counts local changes, so a load that raced one can tell and stand down. */
  const changes = useRef(0);
  const mounted = useRef(true);

  const load = useCallback(async () => {
    const before = changes.current;
    const loaded = await loadDocument<T>(key);
    if (!mounted.current || changes.current !== before) return;
    statusRef.current = loaded.status;
    setStatus(loaded.status);
    if (loaded.status === "owner") setValue(loaded.value);
  }, [key]);

  useEffect(() => {
    mounted.current = true;
    void load();
    return () => {
      mounted.current = false;
    };
  }, [load]);

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
    // Shown again, with nothing of ours waiting to be saved: take the
    // server's copy, which another device may have changed meanwhile.
    const onShow = () => {
      if (document.visibilityState !== "visible") return;
      if (pending.current !== null) return;
      void load();
    };
    window.addEventListener("pagehide", onHide);
    document.addEventListener("visibilitychange", onShow);
    return () => {
      window.removeEventListener("pagehide", onHide);
      document.removeEventListener("visibilitychange", onShow);
      flush(true);
    };
  }, [flush, load]);

  const set = useCallback(
    (next: T | ((current: T | null) => T)) => {
      if (statusRef.current !== "owner") return;
      changes.current += 1;
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
