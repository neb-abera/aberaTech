import { useCallback, useEffect, useState } from "react";

/** A link's rules, as the server stores them. */
export interface LinkRules {
  name: string;
  earliestDate: string | null;
  horizonDays: number;
  leadMinutes: number;
  appointmentMinutes: number;
  showsQueue: boolean;
  active: boolean;
}

export interface BookingLink extends LinkRules {
  id: string | null;
  isDefault: boolean;
  bookings: number;
  /** The key, for the owner. Null when revoked or made before keys were kept. */
  key: string | null;
  issuedAt: string | null;
  revokedAt: string | null;
}

interface LinkList {
  default: BookingLink;
  links: BookingLink[];
}

/** A key, which the server shows once. */
export interface NewKey {
  link: BookingLink;
  key: string;
}

type Outcome = { error: string | null };

async function errorOf(response: Response, fallback: string): Promise<string> {
  const body = (await response.json().catch(() => null)) as {
    error?: string;
  } | null;
  return body?.error ?? fallback;
}

export function useBookingLinks(enabled: boolean) {
  const [list, setList] = useState<LinkList | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!enabled) return;
    const response = await fetch("/api/scheduling/admin/links");
    if (!response.ok) {
      setError(`Could not read your links (${response.status}).`);
      return;
    }
    setList((await response.json()) as LinkList);
    setError(null);
  }, [enabled]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const send = useCallback(
    async (
      method: string,
      url: string,
      body: unknown,
      fallback: string,
    ): Promise<Response | string> => {
      const response = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      return response.ok ? response : errorOf(response, fallback);
    },
    [],
  );

  const create = useCallback(
    async (rules: LinkRules): Promise<NewKey | string> => {
      const result = await send(
        "POST",
        "/api/scheduling/admin/links",
        rules,
        "Could not make the link.",
      );
      if (typeof result === "string") return result;
      const made = (await result.json()) as NewKey;
      await reload();
      return made;
    },
    [send, reload],
  );

  const update = useCallback(
    async (id: string, rules: LinkRules): Promise<Outcome> => {
      // The switch and fields already show the change. Put it in the list at
      // once, so a reload that read the server before the save cannot undo it.
      setList((current) =>
        current
          ? {
              ...current,
              links: current.links.map((link) =>
                link.id === id ? { ...link, ...rules } : link,
              ),
            }
          : current,
      );
      const result = await send(
        "PUT",
        `/api/scheduling/admin/links/${id}`,
        rules,
        "Could not save the link.",
      );
      await reload();
      return { error: typeof result === "string" ? result : null };
    },
    [send, reload],
  );

  const saveDefault = useCallback(
    async (rules: LinkRules): Promise<Outcome> => {
      const result = await send(
        "PUT",
        "/api/scheduling/admin/links/default",
        rules,
        "Could not save the default rules.",
      );
      await reload();
      return { error: typeof result === "string" ? result : null };
    },
    [send, reload],
  );

  const newKey = useCallback(
    async (id: string): Promise<NewKey | string> => {
      const result = await send(
        "POST",
        `/api/scheduling/admin/links/${id}/key`,
        undefined,
        "Could not make a new key.",
      );
      if (typeof result === "string") return result;
      const made = (await result.json()) as NewKey;
      await reload();
      return made;
    },
    [send, reload],
  );

  const revoke = useCallback(
    async (id: string): Promise<Outcome> => {
      const result = await send(
        "POST",
        `/api/scheduling/admin/links/${id}/revoke`,
        undefined,
        "Could not revoke the link.",
      );
      await reload();
      return { error: typeof result === "string" ? result : null };
    },
    [send, reload],
  );

  return { list, error, create, update, saveDefault, newKey, revoke };
}
