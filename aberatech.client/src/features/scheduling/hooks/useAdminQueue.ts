import { useCallback, useEffect, useState } from "react";
import type { AdminMessages } from "../core/messages";

export interface AdminEntry {
  id: string;
  position: number;
  displayName: string;
  phoneE164: string;
  state: string;
  projectedStart: string | null;
  expectedMinutes: number;
}

export interface AdminQueue {
  sessionId: string | null;
  name: string | null;
  open: boolean;
  closesAt: string | null;
  entries: AdminEntry[];
}

export interface CalendarStatus {
  connected: boolean;
  email: string | null;
  calendarId: string | null;
}

interface Admin {
  configured: boolean;
  calendar: CalendarStatus | null;
  disconnectCalendar: () => Promise<void>;
  signedIn: boolean;
  email: string | null;
  queue: AdminQueue | null;
  messages: AdminMessages | null;
  error: string | null;
  loading: boolean;
  openSession: (name: string, hoursOpen: number) => Promise<string | null>;
  closeSession: () => Promise<void>;
  advance: (
    entryId: string,
    action: "start" | "done" | "no-show",
  ) => Promise<void>;
  setDuration: (entryId: string, minutes: number) => Promise<void>;
  refresh: () => Promise<void>;
}

/** Refreshed often enough that two devices running the same queue stay close. */
const PollMs = 10_000;

export function useAdminQueue(): Admin {
  const [configured, setConfigured] = useState(true);
  const [signedIn, setSignedIn] = useState(false);
  const [email, setEmail] = useState<string | null>(null);
  const [queue, setQueue] = useState<AdminQueue | null>(null);
  const [messages, setMessages] = useState<AdminMessages | null>(null);
  const [calendar, setCalendar] = useState<CalendarStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    try {
      const who = await fetch("/api/scheduling/admin/me");

      if (!who.ok) {
        // The admin surface is not configured on this deployment at all.
        setSignedIn(false);
        return;
      }

      const identity = (await who.json()) as {
        configured: boolean;
        signedIn: boolean;
        email: string | null;
      };
      setConfigured(identity.configured);
      setSignedIn(identity.signedIn);
      setEmail(identity.email);

      if (!identity.signedIn) {
        setQueue(null);
        return;
      }

      const [queueResponse, calendarResponse, messagesResponse] =
        await Promise.all([
          fetch("/api/scheduling/admin/queue"),
          fetch("/api/scheduling/admin/calendar"),
          fetch("/api/scheduling/admin/messages"),
        ]);

      if (!queueResponse.ok)
        throw new Error(`Could not read the queue (${queueResponse.status}).`);

      setQueue((await queueResponse.json()) as AdminQueue);

      if (calendarResponse.ok) {
        setCalendar((await calendarResponse.json()) as CalendarStatus);
      }

      if (messagesResponse.ok) {
        setMessages((await messagesResponse.json()) as AdminMessages);
      }

      setError(null);
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), PollMs);
    return () => clearInterval(timer);
  }, [refresh]);

  const openSession = useCallback(
    async (name: string, hoursOpen: number) => {
      const response = await fetch("/api/scheduling/admin/session", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, hoursOpen }),
      });

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as {
          error?: string;
        } | null;
        return body?.error ?? "Could not open the queue.";
      }

      await refresh();
      return null;
    },
    [refresh],
  );

  const closeSession = useCallback(async () => {
    await fetch("/api/scheduling/admin/session/close", { method: "POST" });
    await refresh();
  }, [refresh]);

  const advance = useCallback(
    async (entryId: string, action: "start" | "done" | "no-show") => {
      await fetch(`/api/scheduling/admin/queue/${entryId}/${action}`, {
        method: "POST",
      });
      await refresh();
    },
    [refresh],
  );

  const setDuration = useCallback(
    async (entryId: string, minutes: number) => {
      await fetch(`/api/scheduling/admin/queue/${entryId}/duration`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ minutes }),
      });
      await refresh();
    },
    [refresh],
  );

  const disconnectCalendar = useCallback(async () => {
    await fetch("/api/scheduling/admin/calendar/disconnect", {
      method: "POST",
    });
    await refresh();
  }, [refresh]);

  return {
    configured,
    calendar,
    disconnectCalendar,
    signedIn,
    email,
    queue,
    messages,
    error,
    loading,
    openSession,
    closeSession,
    advance,
    setDuration,
    refresh,
  };
}
