/**
 * The calendar alerts, from the page's side: one read of the state, and
 * the buttons and the settings form, which each answer with the state the
 * server stored.
 *
 * A 401 or 403 is the answer "you are a visitor", not an error, and the
 * page shows the sign-in button on it. A deployment missing a secret
 * answers `configured: false` with the names it lacks, never the values.
 */

import { requestJson } from "../../../site/earlyRequest";

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

/** What the alerts run on: the configuration's values until the owner saves. */
export interface AlertSettings {
  /** 0 normal, 1 high, 2 emergency. */
  priority: 0 | 1 | 2;
  /** Seconds between sounds. Priority 2 only. */
  repeatSeconds: number;
  /** When an unacknowledged message stops. Priority 2 only. */
  stopAfterMinutes: number;
  /** A Pushover sound name, or "" for the phone's own default. */
  sound: string;
  defaultLeadMinutes: number;
  pollMinutes: number;
  lookaheadHours: number;
  includeAllDay: boolean;
  /** The zone when the calendar names none. "" is UTC. */
  timeZone: string;
  ownerEmails: string[];
}

export interface Bound {
  min: number;
  max: number;
}

/** What the form's inputs accept. The server checks the same numbers. */
export interface SettingsBounds {
  repeatSeconds: Bound;
  stopAfterMinutes: Bound;
  defaultLeadMinutes: Bound;
  pollMinutes: Bound;
  lookaheadHours: Bound;
  maxOwnerEmails: number;
  maxEmergencySounds: number;
  sounds: string[];
}

export interface AlertsState {
  /** The calendar's own zone, which every time on the page is written in. */
  timeZone: string;
  pollMinutes: number;
  defaultLeadMinutes: number;
  settings: AlertSettings;
  bounds: SettingsBounds;
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
      reason:
        | "visitor"
        | "throttled"
        | "refused"
        | "network"
        | "pushover"
        | "invalid";
      detail?: string;
      /** For "invalid": the server's message for each field it refused. */
      errors?: Record<string, string[]>;
    };

type StateBody = AlertsState & { configured?: boolean; missing?: string[] };

/** The first thing the page asks. The head script asks it first (site/earlyRequest.ts). */
export const alertsStatusUrl = "/api/alerts/status";

export async function fetchAlerts(): Promise<AlertsView> {
  try {
    const response = await requestJson(alertsStatusUrl);
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

/** One message to the phone, sent the way an event's alert is, whatever the mute says. */
export async function sendTestAlert(): Promise<ActionResult> {
  const result = await post("/api/alerts/test");
  return result.ok ? { ok: true } : result;
}

/**
 * One listed alert's own text, titled "Test: ", with the saved settings.
 * Claims nothing, so the real alert still goes at its time.
 */
export async function sendEventTest(key: string): Promise<ActionResult> {
  const result = await post("/api/alerts/test-event", { key });
  return result.ok ? { ok: true } : result;
}

/** The whole form. A refused field comes back in `errors`, keyed by its name. */
export function saveAlertSettings(
  settings: AlertSettings,
): Promise<ActionResult> {
  return send("PUT", "/api/alerts/settings", settings);
}

function post(path: string, body?: unknown): Promise<ActionResult> {
  return send("POST", path, body);
}

async function send(
  method: "POST" | "PUT",
  path: string,
  body?: unknown,
): Promise<ActionResult> {
  try {
    const response = await fetch(path, {
      method,
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
    if (
      response.status === 400 &&
      response.headers.get("content-type")?.includes("json")
    ) {
      const problem = (await response.json()) as {
        errors?: Record<string, string[]>;
      };
      if (problem.errors)
        return { ok: false, reason: "invalid", errors: problem.errors };
    }
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
