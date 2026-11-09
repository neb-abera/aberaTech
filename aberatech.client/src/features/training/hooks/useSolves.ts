import { useEffect, useState } from "react";
import type { Contest } from "../components/TodayView";
import type { JudgeSolves, Solves } from "../core/progress";
import { noSolves } from "../core/progress";

export interface JudgeLine {
  handle: string;
  ok: boolean;
  error?: string | null;
  rating?: number | null;
  maxRating?: number | null;
  rank?: string | null;
  ratedContests: number;
  solved: Record<string, string>;
  readAt: string;
}

export interface SolvesAnswer {
  status: "idle" | "loading" | "ready" | "error";
  solves: Solves;
  codeforces: JudgeLine | null;
  atcoder: JudgeLine | null;
  contests: Contest[];
}

const idle: SolvesAnswer = {
  status: "idle",
  solves: noSolves,
  codeforces: null,
  atcoder: null,
  contests: [],
};

const asJudge = (line: JudgeLine | null): JudgeSolves | null =>
  line?.ok ? { solved: line.solved, ratedContests: line.ratedContests } : null;

/**
 * What Codeforces and AtCoder Problems say about the owner's handles,
 * asked of the server once the owner is known. A judge that did not answer
 * is null in `solves`, and its items fall back to manual ticks.
 */
export function useSolves(
  owner: boolean,
  codeforces: string,
  atcoder: string,
): SolvesAnswer {
  const [answer, setAnswer] = useState<SolvesAnswer>(idle);

  useEffect(() => {
    if (!owner) return;
    let canceled = false;
    setAnswer((current) => ({ ...current, status: "loading" }));
    const query = new URLSearchParams();
    if (codeforces) query.set("codeforces", codeforces);
    if (atcoder) query.set("atcoder", atcoder);
    fetch(`/api/training/solves?${query}`, { credentials: "same-origin" })
      .then(async (response) => {
        if (!response.ok) throw new Error(String(response.status));
        const body = (await response.json()) as {
          configured: boolean;
          codeforces?: JudgeLine | null;
          atcoder?: JudgeLine | null;
          contests?: Contest[];
        };
        if (canceled) return;
        const cf = body.codeforces ?? null;
        const ac = body.atcoder ?? null;
        setAnswer({
          status: "ready",
          solves: { codeforces: asJudge(cf), atcoder: asJudge(ac) },
          codeforces: cf,
          atcoder: ac,
          contests: body.contests ?? [],
        });
      })
      .catch(() => {
        if (!canceled) setAnswer({ ...idle, status: "error" });
      });
    return () => {
      canceled = true;
    };
  }, [owner, codeforces, atcoder]);

  return answer;
}
