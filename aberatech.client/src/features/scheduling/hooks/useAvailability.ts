import { useCallback, useEffect, useState } from 'react';

export interface AvailabilityDay {
  day: number;
  startsAt: string;
  endsAt: string;
  active: boolean;
}

export interface AvailabilityWeek {
  zoneId: string;
  days: AvailabilityDay[];
}

interface Availability {
  week: AvailabilityWeek | null;
  loading: boolean;
  error: string | null;
  saved: boolean;
  setWeek: (week: AvailabilityWeek) => void;
  save: () => Promise<void>;
  reload: () => Promise<void>;
}

export function useAvailability(enabled: boolean): Availability {
  const [week, setWeek] = useState<AvailabilityWeek | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const reload = useCallback(async () => {
    if (!enabled) {
      setLoading(false);
      return;
    }

    try {
      const response = await fetch('/api/scheduling/admin/availability');
      if (!response.ok) throw new Error(`Could not read your hours (${response.status}).`);

      setWeek((await response.json()) as AvailabilityWeek);
      setError(null);
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setLoading(false);
    }
  }, [enabled]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const save = useCallback(async () => {
    if (!week) return;

    setSaved(false);

    const response = await fetch('/api/scheduling/admin/availability', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(week)
    });

    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { error?: string } | null;
      setError(body?.error ?? 'Could not save your hours.');
      return;
    }

    setError(null);
    setSaved(true);
    await reload();
  }, [week, reload]);

  return { week, loading, error, saved, setWeek, save, reload };
}
