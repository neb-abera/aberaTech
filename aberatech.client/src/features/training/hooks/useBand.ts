import { useEffect, useState } from "react";
import type { BandList } from "../core/saad";

export interface BandAnswer extends BandList {
  status: "idle" | "loading" | "ready" | "error";
  ok: boolean;
  error?: string | null;
}

const idle: BandAnswer = {
  status: "idle",
  ok: false,
  from: 0,
  to: 0,
  problems: [],
};

/**
 * Codeforces problemset problems rated `rating` to `rating` + 200, as the
 * server read them from the Codeforces API, with no tags. Asked only once
 * the owner is known. The server holds the problemset for an hour.
 */
export function useBand(owner: boolean, rating: number): BandAnswer {
  const [answer, setAnswer] = useState<BandAnswer>(idle);
  useEffect(() => {
    if (!owner) return;
    let canceled = false;
    setAnswer((current) => ({ ...current, status: "loading" }));
    fetch(`/api/training/band?rating=${rating}`, {
      credentials: "same-origin",
    })
      .then(async (response) => {
        if (!response.ok) throw new Error(String(response.status));
        const body = (await response.json()) as Omit<BandAnswer, "status">;
        if (!canceled) setAnswer({ ...body, status: "ready" });
      })
      .catch(() => {
        if (!canceled) setAnswer({ ...idle, status: "error" });
      });
    return () => {
      canceled = true;
    };
  }, [owner, rating]);
  return answer;
}
