/**
 * The calendar alerts, from the page's side: one read of the state, and
 * five buttons that each answer with the state the server stored.
 *
 * A 401 or 403 is the answer "you are a visitor", not an error, and the
 * page shows the sign-in button on it. A deployment missing a secret
 * answers `configured: false` with the names it lacks, never the values.
 */

export interface AlertItem {
  /** The event's UID and the occurrence's start: what Skip sends back. */
  key: string;
  title: string;
  location: string | null;
  startsAt: string;
  alertAt: string;
  /** "reminder": the event's own reminder. "default": the default lead. */
  source: "reminder" | "default";
  skipped: boolean;
  muted: boolean;
}

export interface LastSend {
  at: string;
  title: string;
  outcome: string;
}

export interface AlertsState {
  /** The calendar's own zone, which every time on the page is written in. */
  timeZone: string;
  pollMinutes: number;
  defaultLeadMinutes: number;
  mutedUntil: string | null;
  lastFetchAt: string | null;
  lastFetchError: string | null;
  lastSuccessAt: string | null;
  lastSend: LastSend | null;
  alerts: AlertItem[];
}

export type AlertsView =
  | { status: "visitor" }
  | { status: "unconfigured"; missing: string[] }
  | { status: "owner"; state: AlertsState }
  | { status: "error" };

export type ActionResult =
  | { ok: true; state?: AlertsState }
  | {
      ok: false;
      reason: "visitor" | "throttled" | "refused" | "network" | "pushover";
      detail?: string;
    };

type StateBody = AlertsState & { configured?: boolean; missing?: string[] };

export async function fetchAlerts(): Promise<AlertsView> {
  try {
    const response = await fetch("/api/alerts/status", {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    });
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (!response.ok) return { status: "error" };
    if (!response.headers.get("content-type")?.includes("application/json"))
      return { status: "error" };
    const body = (await response.json()) as StateBody;
    if (body.configured !== true)
      return { status: "unconfigured", missing: body.missing ?? [] };
    return { status: "owner", state: toState(body) };
  } catch {
    return { status: "error" };
  }
}

export function muteAlerts(until: "hour" | "morning"): Promise<ActionResult> {
  return post("/api/alerts/mute", { until });
}

export function unmuteAlerts(): Promise<ActionResult> {
  return post("/api/alerts/unmute");
}

export function skipAlert(key: string): Promise<ActionResult> {
  return post("/api/alerts/skip", { key });
}

export function unskipAlert(key: string): Promise<ActionResult> {
  return post("/api/alerts/unskip", { key });
}

/** One priority 1 message to the phone, whatever the mute says. */
export async function sendTestAlert(): Promise<ActionResult> {
  const result = await post("/api/alerts/test");
  return result.ok ? { ok: true } : result;
}

async function post(path: string, body?: unknown): Promise<ActionResult> {
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
    if (response.status === 502)
      return {
        ok: false,
        reason: "pushover",
        detail: (await response.text()).slice(0, 200),
      };
    if (!response.ok) return { ok: false, reason: "refused" };
    const answer = (await response.json()) as StateBody | { sent: boolean };
    return "alerts" in answer
      ? { ok: true, state: toState(answer) }
      : { ok: true };
  } catch {
    return { ok: false, reason: "network" };
  }
}

function toState(body: StateBody): AlertsState {
  const { configured: _configured, missing: _missing, ...state } = body;
  return state;
}

const formats = new Map<string, Intl.DateTimeFormat>();

function formatFor(timeZone: string): Intl.DateTimeFormat {
  const cached = formats.get(timeZone);
  if (cached) return cached;
  let format: Intl.DateTimeFormat;
  try {
    format = new Intl.DateTimeFormat("en-US", {
      timeZone,
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
      timeZoneName: "short",
    });
  } catch {
    // A zone this browser's copy of the database does not know.
    format = formatFor("UTC");
  }
  formats.set(timeZone, format);
  return format;
}

/** "Wed, Oct 28, 9:00 AM EDT": an instant in the calendar's zone, naming the zone. */
export function formatWhen(iso: string, timeZone: string): string {
  // ICU puts a narrow no-break space before AM and PM. A plain space reads
  // the same and matches what a person types when searching the page.
  return formatFor(timeZone).format(new Date(iso)).replace(/[  ]/g, " ");
}
