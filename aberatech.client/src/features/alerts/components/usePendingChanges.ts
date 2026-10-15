import * as React from "react";

/**
 * Where a change the page already shows stands with the server: sent and
 * not yet answered, saved, or waiting for a connection to send it.
 */
export type SyncStatus = "sending" | "saved" | "waiting";

/** What one send came to. Offline keeps the change and tries again. */
export type SendOutcome =
  | { ok: true }
  | { ok: false; offline: true }
  | { ok: false; offline: false; problem: string };

/** How often a change waiting for a connection is sent again. */
export const retryEveryMs = 15_000;
/** How long "Saved" shows before it fades. */
export const savedForMs = 2_000;

export interface Pending<V> {
  value: V;
  status: SyncStatus;
}

/**
 * Changes a control shows at once, before the server answers. Each key
 * (a routine, an event, the mute) holds the last value pressed. A press
 * while one is in flight is sent when that one answers, so only the
 * latest value goes. No connection, or a server error, keeps the value and
 * sends it again when the browser is back online and every 15 s. A
 * refusal drops it, so the control shows the server's value again.
 */
export function usePendingChanges<V>(
  send: (key: string, value: V) => Promise<SendOutcome>,
  onRefused: (key: string, problem: string) => void,
) {
  const [pending, setPending] = React.useState<Record<string, Pending<V>>>({});
  const desired = React.useRef(new Map<string, V>());
  const inFlight = React.useRef(new Set<string>());
  const fades = React.useRef(new Map<string, number>());
  const alive = React.useRef(true);
  const sendRef = React.useRef(send);
  const refusedRef = React.useRef(onRefused);

  React.useEffect(() => {
    sendRef.current = send;
    refusedRef.current = onRefused;
  });

  React.useEffect(() => {
    alive.current = true;
    const timers = fades.current;
    return () => {
      alive.current = false;
      for (const timer of timers.values()) window.clearTimeout(timer);
      timers.clear();
    };
  }, []);

  const put = React.useCallback((key: string, entry: Pending<V> | null) => {
    setPending((current) => {
      const next = { ...current };
      if (entry) next[key] = entry;
      else delete next[key];
      return next;
    });
  }, []);

  const stopFade = React.useCallback((key: string) => {
    const timer = fades.current.get(key);
    if (timer !== undefined) window.clearTimeout(timer);
    fades.current.delete(key);
  }, []);

  const flush = React.useCallback(
    async (key: string, retry = false): Promise<void> => {
      if (inFlight.current.has(key) || !desired.current.has(key)) return;
      const value = desired.current.get(key) as V;
      inFlight.current.add(key);
      // A retry keeps saying it waits, so the line does not flicker.
      if (!retry) put(key, { value, status: "sending" });
      let outcome: SendOutcome;
      try {
        outcome = await sendRef.current(key, value);
      } catch {
        outcome = { ok: false, offline: true };
      }
      inFlight.current.delete(key);
      if (!alive.current) return;
      if (desired.current.get(key) !== value) {
        // Pressed again while this one was out: send the latest.
        await flush(key);
        return;
      }
      if (outcome.ok) {
        desired.current.delete(key);
        put(key, { value, status: "saved" });
        stopFade(key);
        fades.current.set(
          key,
          window.setTimeout(() => {
            fades.current.delete(key);
            if (!desired.current.has(key)) put(key, null);
          }, savedForMs),
        );
        return;
      }
      if (outcome.offline) {
        put(key, { value, status: "waiting" });
        return;
      }
      desired.current.delete(key);
      put(key, null);
      refusedRef.current(key, outcome.problem);
    },
    [put, stopFade],
  );

  const change = React.useCallback(
    (key: string, value: V) => {
      desired.current.set(key, value);
      stopFade(key);
      put(key, { value, status: "sending" });
      void flush(key);
    },
    [flush, put, stopFade],
  );

  const waiting = Object.values(pending).some(
    (entry) => entry.status === "waiting",
  );

  React.useEffect(() => {
    if (!waiting) return;
    const retry = () => {
      for (const key of desired.current.keys()) void flush(key, true);
    };
    window.addEventListener("online", retry);
    const timer = window.setInterval(retry, retryEveryMs);
    return () => {
      window.removeEventListener("online", retry);
      window.clearInterval(timer);
    };
  }, [waiting, flush]);

  return { pending, change };
}
