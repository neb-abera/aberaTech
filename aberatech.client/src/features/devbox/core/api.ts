/**
 * The dev box, from the page's side: one read of its power state and one
 * request to start it.
 *
 * A 401 or 403 is the answer "you are a visitor", not an error, and the page
 * shows the sign-in button on it. A deployment with no subscription answers
 * `configured: false`, and the page says so instead of offering a button
 * that would do nothing.
 */

/** What the box's agent last reported, as the status route relays it. */
export type AgentReport =
  | { seen: false }
  | {
      seen: true;
      seenSecondsAgo: number;
      remoteControl: string | null;
      sessions: number;
      load: number;
      uptimeSeconds: number;
      holdUntil: string | null;
      environmentUrl: string | null;
      pending: { holdMinutes: number | null; park: boolean };
    };

export type DevBoxStatus =
  | { status: "visitor" }
  | { status: "unconfigured" }
  | { status: "owner"; power: string; agent: AgentReport | null }
  | { status: "error" };

export type StartResult =
  | { ok: true }
  | { ok: false; reason: "visitor" | "throttled" | "azure" | "network" };

export async function fetchDevBoxStatus(): Promise<DevBoxStatus> {
  try {
    const response = await fetch("/api/devbox/status", {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    });
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (!response.ok) return { status: "error" };
    // A deployment with no owner answers the SPA shell, not JSON.
    if (!response.headers.get("content-type")?.includes("application/json"))
      return { status: "visitor" };
    const body = (await response.json()) as {
      configured?: boolean;
      power?: string | null;
      agent?: AgentReport | null;
    };
    if (body.configured !== true) return { status: "unconfigured" };
    return {
      status: "owner",
      power: body.power ?? "unknown",
      agent: body.agent ?? null,
    };
  } catch {
    return { status: "error" };
  }
}

export function startDevBox(): Promise<StartResult> {
  return post("/api/devbox/start");
}

/** Keep the box up for `minutes`, through the idle watchdog and the nightly check. */
export function holdDevBox(minutes: number): Promise<StartResult> {
  return post("/api/devbox/hold", { minutes });
}

/** Park it now. The box does it itself on its next heartbeat. */
export function parkDevBox(): Promise<StartResult> {
  return post("/api/devbox/park");
}

async function post(path: string, body?: unknown): Promise<StartResult> {
  try {
    const response = await fetch(path, {
      method: "POST",
      credentials: "same-origin",
      ...(body === undefined
        ? {}
        : {
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
          }),
    });
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (response.status === 429) return { ok: false, reason: "throttled" };
    if (!response.ok) return { ok: false, reason: "azure" };
    return { ok: true };
  } catch {
    return { ok: false, reason: "network" };
  }
}

/** Whether a power state, as Azure spells it, means the box can take a session. */
export function isRunning(power: string): boolean {
  return power === "running";
}

/** Whether the box is parked, which is the one state the Start button is for. */
export function isParked(power: string): boolean {
  return power === "deallocated" || power === "stopped";
}

/** Whether Azure is between states and the page should keep asking. */
export function isInTransit(power: string): boolean {
  return (
    power === "starting" || power === "deallocating" || power === "stopping"
  );
}
