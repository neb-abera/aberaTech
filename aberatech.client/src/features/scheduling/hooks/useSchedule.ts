import { useCallback, useEffect, useRef, useState } from "react";
import { viewerZone } from "../core/format";
import type {
  BookingConfirmation,
  MyPlace,
  ScheduleState,
} from "../core/types";

/**
 * How often the page asks where the queue has got to.
 *
 * Polling rather than a websocket, deliberately. Twenty-eight people on a
 * fifteen second interval is under two requests a second, which is nothing,
 * and it keeps the server stateless: the moment live updates are pushed, a
 * second container app replica needs a Redis backplane to agree with the first.
 * That is a real recurring cost to buy an improvement nobody can perceive here.
 */
const PollMs = 15_000;

/**
 * Where this browser remembers its place in the line.
 *
 * The entry id is a v4 GUID and acts as the capability to read and cancel that
 * one entry, so it stays in this browser and is never put in a URL, where it
 * would leak through history, bookmarks and referer headers.
 */
const StorageKey = "aberatech.scheduling.entry";

interface Schedule {
  state: ScheduleState | null;
  place: MyPlace | null;
  error: string | null;
  loading: boolean;
  join: (
    name: string,
    phone: string,
    smsConsent: boolean,
  ) => Promise<string | null>;
  leave: () => Promise<void>;
  book: (
    startsAt: string,
    name: string,
    phone: string,
    smsConsent: boolean,
    email: string,
  ) => Promise<{ error: string | null }>;
  booking: BookingConfirmation | null;
  selectDate: (date: string) => void;
}

export function useSchedule(): Schedule {
  const [state, setState] = useState<ScheduleState | null>(null);
  const [place, setPlace] = useState<MyPlace | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [booking, setBooking] = useState<BookingConfirmation | null>(null);
  const [loading, setLoading] = useState(true);
  const entryId = useRef<string | null>(localStorage.getItem(StorageKey));

  // Which day's times to fetch. Null lets the server pick the first day with
  // anything free, so the page opens on a day worth looking at.
  const [date, setDate] = useState<string | null>(null);

  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      try {
        const query = new URLSearchParams({ zone: viewerZone() });
        if (date !== null) query.set("date", date);

        const response = await fetch(`/api/scheduling/state?${query}`, {
          signal,
        });
        if (!response.ok)
          throw new Error(`The schedule is unavailable (${response.status}).`);
        setState((await response.json()) as ScheduleState);
        setError(null);

        if (entryId.current) {
          const mine = await fetch(`/api/scheduling/queue/${entryId.current}`, {
            signal,
          });
          if (mine.ok) {
            setPlace((await mine.json()) as MyPlace);
          } else if (mine.status === 404) {
            // The session was cleared or the entry removed. Forget it rather than
            // showing a place in a queue that no longer exists.
            localStorage.removeItem(StorageKey);
            entryId.current = null;
            setPlace(null);
          }
        }
      } catch (caught) {
        if ((caught as Error).name !== "AbortError") {
          setError((caught as Error).message);
        }
      } finally {
        setLoading(false);
      }
    },
    [date],
  );

  useEffect(() => {
    const controller = new AbortController();
    void refresh(controller.signal);

    const timer = setInterval(() => void refresh(controller.signal), PollMs);

    return () => {
      controller.abort();
      clearInterval(timer);
    };
  }, [refresh]);

  const join = useCallback(
    async (name: string, phone: string, smsConsent: boolean) => {
      const response = await fetch("/api/scheduling/queue", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, phone, zoneId: viewerZone(), smsConsent }),
      });

      if (response.status === 429) {
        return "Too many attempts. Please wait a minute and try again.";
      }

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as {
          error?: string;
        } | null;
        return body?.error ?? "Could not join the queue.";
      }

      const { id } = (await response.json()) as { id: string };
      localStorage.setItem(StorageKey, id);
      entryId.current = id;
      await refresh();
      return null;
    },
    [refresh],
  );

  const leave = useCallback(async () => {
    if (!entryId.current) return;

    await fetch(`/api/scheduling/queue/${entryId.current}`, {
      method: "DELETE",
    });
    localStorage.removeItem(StorageKey);
    entryId.current = null;
    setPlace(null);
    await refresh();
  }, [refresh]);

  const book = useCallback(
    async (
      startsAt: string,
      name: string,
      phone: string,
      smsConsent: boolean,
      email: string,
    ) => {
      const response = await fetch("/api/scheduling/book", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          startsAt,
          name,
          phone,
          zoneId: viewerZone(),
          smsConsent,
          email,
        }),
      });

      if (response.status === 429) {
        return {
          error: "Too many attempts. Please wait a minute and try again.",
        };
      }

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as {
          error?: string;
        } | null;
        // A 409 here is the database refusing a double booking, which is a
        // normal outcome of two people picking the same time, not a fault.
        await refresh();
        return { error: body?.error ?? "Could not book that time." };
      }

      setBooking((await response.json()) as BookingConfirmation);
      await refresh();
      return { error: null };
    },
    [refresh],
  );

  const selectDate = useCallback((next: string) => setDate(next), []);

  return {
    state,
    place,
    error,
    loading,
    join,
    leave,
    book,
    booking,
    selectDate,
  };
}
