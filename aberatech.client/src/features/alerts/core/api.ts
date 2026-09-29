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
  /** Marked #critical in the calendar. The mark is left off the title. */
  critical: boolean;
  /** What this occurrence will send. */
  type: AlertType;
  /** "set" on this page, "critical" from the calendar's mark, or "default". */
  typeFrom: "set" | "critical" | "default";
  /** Answered on a paired phone or in a browser. Nothing more is sent. */
  acknowledged: boolean;
  acknowledgedAt: string | null;
  acknowledgedVia: "phone" | "browser" | null;
}

/** An alarm uses the alarm settings. A notification sounds once. None sends nothing. */
export type AlertType = "none" | "notification" | "alarm";

export interface LastSend {
  at: string;
  title: string;
  outcome: string;
}

/** What the alerts run on: the configuration's values until the owner saves. */
export interface AlertSettings {
  /** Seconds between an alarm's Pushover repeats. An alarm always repeats until acknowledged. */
  repeatSeconds: number;
  /** When an unacknowledged alarm stops. */
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
  /** A notification's priority: 0 normal, 1 high. It sounds once. */
  notificationPriority: 0 | 1;
  /** A notification's Pushover sound, or "" for the phone's own default. */
  notificationSound: string;
  /** What an unmarked event with no choice sends. */
  defaultType: "none" | "notification";
  /** Seconds after an alarm's time before Pushover follows, so a paired phone rings first. */
  backupDelaySeconds: number;
}

export interface Bound {
  min: number;
  max: number;
}

/** One sound the Pushover account offers. Custom is one the owner uploaded. */
export interface PushoverSound {
  name: string;
  description: string;
  custom: boolean;
}

/** What the form's inputs accept. The server checks the same numbers. */
export interface SettingsBounds {
  repeatSeconds: Bound;
  stopAfterMinutes: Bound;
  defaultLeadMinutes: Bound;
  pollMinutes: Bound;
  lookaheadHours: Bound;
  backupDelaySeconds: Bound;
  maxOwnerEmails: number;
  maxEmergencySounds: number;
  /** The account's uploads first, then Pushover's built-ins. */
  sounds: PushoverSound[];
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
  /**
   * Set only in the answer to a type change or a new event: why Google
   * Calendar was not changed. Null when it was, or when nothing needed
   * changing.
   */
  calendarWrite?: string | null;
  /**
   * Whether the server pushes phones. Missing lists the secret names that
   * are not set, never their values.
   */
  push?: PushStatus;
}

/** Phone pushes: on when all three secrets are set. */
export interface PushStatus {
  on: boolean;
  missing: string[];
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

/** One alarm to the phone, with the alarm settings, whatever the mute says. */
export async function sendTestAlert(): Promise<ActionResult> {
  const result = await post("/api/alerts/test");
  return result.ok ? { ok: true } : result;
}

/** One notification to the phone, with the notification settings, whatever the mute says. */
export async function sendTestNotification(): Promise<ActionResult> {
  const result = await post("/api/alerts/test-notification");
  return result.ok ? { ok: true } : result;
}

/**
 * The type for a listed alert's event, every occurrence of it. "default"
 * drops the choice: the event follows its #critical mark and the default.
 */
export function setEventType(
  key: string,
  type: AlertType | "default",
): Promise<ActionResult> {
  return send("PUT", "/api/alerts/event-type", { key, type });
}

/**
 * One listed alert's own text, titled "Test: ", sent as its type.
 * Claims nothing, so the real alert still goes at its time.
 */
export async function sendEventTest(key: string): Promise<ActionResult> {
  const result = await post("/api/alerts/test-event", { key });
  return result.ok ? { ok: true } : result;
}

/**
 * Acknowledge a due alarm: it stops ringing here, on a paired phone, and
 * Pushover stops repeating it. The first acknowledgement stands.
 */
export function acknowledgeAlert(
  key: string,
  via: "phone" | "browser",
): Promise<ActionResult> {
  return post("/api/alerts/ack", { key, via });
}

/** A new event on the calendar the alerts read. */
export interface NewEvent {
  title: string;
  /** An instant with its offset, e.g. from Date.toISOString(). */
  startsAt: string;
  durationMinutes: number;
  location: string | null;
  type: AlertType;
  /** Minutes before the start. Left out, the server uses the default lead. */
  leadMinutes?: number;
}

export type CreateResult =
  | { ok: true; state: AlertsState }
  | {
      ok: false;
      reason:
        | "visitor"
        | "throttled"
        | "invalid"
        | "conflict"
        | "google"
        | "refused"
        | "network";
      detail?: string;
      errors?: Record<string, string[]>;
    };

/**
 * Creates the event on Google Calendar and answers with the state, the new
 * event already listed. 409: no calendar with edit access, or not the one
 * the alerts read. 502: Google refused.
 */
export async function createEvent(event: NewEvent): Promise<CreateResult> {
  try {
    const response = await fetch("/api/alerts/events", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(event),
    });
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (response.status === 429) return { ok: false, reason: "throttled" };
    const json = response.headers.get("content-type")?.includes("json");
    if (response.status === 201)
      return { ok: true, state: toState((await response.json()) as StateBody) };
    const problem = json
      ? ((await response.json()) as {
          detail?: string;
          errors?: Record<string, string[]>;
        })
      : {};
    if (response.status === 400 && problem.errors)
      return { ok: false, reason: "invalid", errors: problem.errors };
    if (response.status === 409)
      return { ok: false, reason: "conflict", detail: problem.detail };
    if (response.status === 502)
      return { ok: false, reason: "google", detail: problem.detail };
    return { ok: false, reason: "refused" };
  } catch {
    return { ok: false, reason: "network" };
  }
}

/** A paired phone as the list shows it. The server never sends its token again. */
export interface Device {
  id: string;
  name: string;
  createdAt: string;
  lastSeenAt: string | null;
  /** The phone has registered for pushes. The server never sends the push token. */
  push: boolean;
}

/** The answer to a pairing: the one time the token is shown. */
export interface PairedDevice {
  id: string;
  name: string;
  createdAt: string;
  token: string;
  /** aberaalarms://pair#token=…, which the phone opens. */
  pairUrl: string;
}

export type DevicesResult =
  | { ok: true; devices: Device[] }
  | { ok: false; reason: "visitor" | "refused" | "network" };

export type PairResult =
  | { ok: true; device: PairedDevice }
  | {
      ok: false;
      reason:
        | "visitor"
        | "throttled"
        | "full"
        | "invalid"
        | "refused"
        | "network";
      detail?: string;
    };

export async function listDevices(): Promise<DevicesResult> {
  try {
    const response = await fetch("/api/alerts/devices", {
      credentials: "same-origin",
    });
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (!response.ok) return { ok: false, reason: "refused" };
    return { ok: true, devices: (await response.json()) as Device[] };
  } catch {
    return { ok: false, reason: "network" };
  }
}

/** A name for the list, 1 to 60 characters. The answer carries the token, once. */
export async function pairDevice(name: string): Promise<PairResult> {
  try {
    const response = await fetch("/api/alerts/devices", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    });
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (response.status === 429) return { ok: false, reason: "throttled" };
    if (response.status === 409)
      return {
        ok: false,
        reason: "full",
        detail: (await response.text()).slice(0, 200),
      };
    if (response.status === 400) return { ok: false, reason: "invalid" };
    if (response.status !== 201) return { ok: false, reason: "refused" };
    return { ok: true, device: (await response.json()) as PairedDevice };
  } catch {
    return { ok: false, reason: "network" };
  }
}

/** The phone's token stops working on its next request. */
export async function revokeDevice(id: string): Promise<{
  ok: boolean;
  reason?: "visitor" | "throttled" | "refused" | "network";
}> {
  try {
    const response = await fetch(
      `/api/alerts/devices/${encodeURIComponent(id)}`,
      { method: "DELETE", credentials: "same-origin" },
    );
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (response.status === 429) return { ok: false, reason: "throttled" };
    // Already gone is what was asked for.
    if (response.status === 204 || response.status === 404) return { ok: true };
    return { ok: false, reason: "refused" };
  } catch {
    return { ok: false, reason: "network" };
  }
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

const clocks = new Map<string, Intl.DateTimeFormat>();

/** "08:47": the time of day in the calendar's zone, on a 24-hour clock. */
export function formatClock(iso: string, timeZone: string): string {
  let format = clocks.get(timeZone);
  if (!format) {
    try {
      format = new Intl.DateTimeFormat("en-GB", {
        timeZone,
        hour: "2-digit",
        minute: "2-digit",
        hourCycle: "h23",
      });
    } catch {
      format = new Intl.DateTimeFormat("en-GB", {
        timeZone: "UTC",
        hour: "2-digit",
        minute: "2-digit",
        hourCycle: "h23",
      });
    }
    clocks.set(timeZone, format);
  }
  return format.format(new Date(iso));
}
