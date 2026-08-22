import { useCallback, useEffect, useState } from 'react';

export interface AdminEntry {
  id: string;
  position: number;
  displayName: string;
  phoneE164: string;
  state: string;
  projectedStart: string | null;
}

export interface AdminQueue {
  sessionId: string | null;
  name: string | null;
  open: boolean;
  entries: AdminEntry[];
}

interface Admin {
  configured: boolean;
  signedIn: boolean;
  email: string | null;
  queue: AdminQueue | null;
  error: string | null;
  loading: boolean;
  openSession: (name: string) => Promise<string | null>;
  closeSession: () => Promise<void>;
  advance: (entryId: string, action: 'start' | 'done' | 'no-show') => Promise<void>;
  refresh: () => Promise<void>;
}

/** Refreshed often enough that two devices running the same queue stay close. */
const PollMs = 10_000;

export function useAdminQueue(): Admin {
  const [configured, setConfigured] = useState(true);
  const [signedIn, setSignedIn] = useState(false);
  const [email, setEmail] = useState<string | null>(null);
  const [queue, setQueue] = useState<AdminQueue | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    try {
      const who = await fetch('/api/scheduling/admin/me');

      if (!who.ok) {
        // The admin surface is not configured on this deployment at all.
        setSignedIn(false);
        return;
      }

      const identity = (await who.json()) as { configured: boolean; signedIn: boolean; email: string | null };
      setConfigured(identity.configured);
      setSignedIn(identity.signedIn);
      setEmail(identity.email);

      if (!identity.signedIn) {
        setQueue(null);
        return;
      }

      const response = await fetch('/api/scheduling/admin/queue');
      if (!response.ok) throw new Error(`Could not read the queue (${response.status}).`);

      setQueue((await response.json()) as AdminQueue);
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
    async (name: string) => {
      const response = await fetch('/api/scheduling/admin/session', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name })
      });

      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { error?: string } | null;
        return body?.error ?? 'Could not open the queue.';
      }

      await refresh();
      return null;
    },
    [refresh]
  );

  const closeSession = useCallback(async () => {
    await fetch('/api/scheduling/admin/session/close', { method: 'POST' });
    await refresh();
  }, [refresh]);

  const advance = useCallback(
    async (entryId: string, action: 'start' | 'done' | 'no-show') => {
      await fetch(`/api/scheduling/admin/queue/${entryId}/${action}`, { method: 'POST' });
      await refresh();
    },
    [refresh]
  );

  return { configured, signedIn, email, queue, error, loading, openSession, closeSession, advance, refresh };
}
