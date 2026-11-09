import { useEffect, useState } from "react";
import type { PoolProblem } from "../core/saad";

export interface PoolAnswer {
  status: "idle" | "loading" | "ready" | "error";
  ok: boolean;
  error?: string | null;
  problems: PoolProblem[];
  rows: number;
  readAt?: string;
  source?: string;
}

const idle: PoolAnswer = { status: "idle", ok: false, problems: [], rows: 0 };

/**
 * Mostafa Saad's pool, as the server read it from his sheet. Asked only
 * once the owner is known. The server holds it in memory for an hour.
 */
export function usePool(owner: boolean): PoolAnswer {
  const [answer, setAnswer] = useState<PoolAnswer>(idle);
  useEffect(() => {
    if (!owner) return;
    let canceled = false;
    setAnswer((current) => ({ ...current, status: "loading" }));
    fetch("/api/training/pool", { credentials: "same-origin" })
      .then(async (response) => {
        if (!response.ok) throw new Error(String(response.status));
        const body = (await response.json()) as Omit<PoolAnswer, "status">;
        if (!canceled) setAnswer({ ...body, status: "ready" });
      })
      .catch(() => {
        if (!canceled) setAnswer({ ...idle, status: "error" });
      });
    return () => {
      canceled = true;
    };
  }, [owner]);
  return answer;
}
