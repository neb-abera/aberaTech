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
import type { Countdown } from "./countdowns";
import type { Routine } from "./routines";

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
  /** Part of a repeating event. Edit and Delete then offer "All events". */
  recurring: boolean;
  /** The occurrence's end. Null when the calendar gives none. */
  endsAt: string | null;
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
  /** The sound the paired phone plays for every alarm it rings: a value from `bounds.phoneSounds`. */
  phoneSound: string;
  /** How many minutes the phone's Snooze delays a calendar alarm. */
  phoneSnoozeMinutes: number;
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

/** One sound the phone app carries: the value saved, and its name on the page. */
export interface PhoneSound {
  value: string;
  label: string;
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
  /** The sounds the phone app carries. The page only names them. */
  phoneSounds: PhoneSound[];
  phoneSnoozeMinutes: Bound;
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
  /**
   * Routine alarms, by hour, minute and label. The paired phone rings
   * them. Nothing on this page or on the server rings them.
   */
  routines?: Routine[];
  /**
   * Countdowns, by target and label. /dates and the paired phone show the
   * time left. An older server leaves the field out.
   */
  countdowns?: Countdown[];
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
  const result = await writeEvent("POST", "/api/alerts/events", event, 201);
  if (result.ok) return result;
  const { reason } = result;
  return reason === "gone"
    ? { ok: false, reason: "refused" }
    : { ...result, reason };
}

/** Which occurrences an edit or a deletion is for. */
export type EventScope = "occurrence" | "series";

/** An edit of one listed occurrence, or of every occurrence of its event. */
export interface EventEdit {
  /** The listed alert's key. It goes in the body, never the address. */
  key: string;
  scope: EventScope;
  title: string;
  /** An instant with its offset. */
  startsAt: string;
  /** Left out, the event keeps its length. */
  durationMinutes?: number;
  /** Null clears it. */
  location: string | null;
  /** Left out, the event's reminders stay as they are. */
  leadMinutes?: number;
}

export type EventWriteResult =
  | { ok: true; state: AlertsState }
  | {
      ok: false;
      reason: Exclude<CreateResult, { ok: true }>["reason"] | "gone";
      detail?: string;
      errors?: Record<string, string[]>;
    };

/**
 * Changes the event on Google Calendar and answers with the state, the
 * change already listed. 404: the alert is no longer listed. 409: Google
 * lets only the organiser change it, or no calendar with edit access.
 */
export function editEvent(edit: EventEdit): Promise<EventWriteResult> {
  return writeEvent("PUT", "/api/alerts/events", edit, 200);
}

/** Deletes one occurrence, or the whole event, and answers with the state without it. */
export function deleteEvent(
  key: string,
  scope: EventScope,
): Promise<EventWriteResult> {
  return writeEvent("POST", "/api/alerts/events/delete", { key, scope }, 200);
}

async function writeEvent(
  method: "POST" | "PUT",
  path: string,
  body: unknown,
  success: number,
): Promise<EventWriteResult> {
  try {
    const response = await fetch(path, {
      method,
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (response.status === 401 || response.status === 403)
      return { ok: false, reason: "visitor" };
    if (response.status === 429) return { ok: false, reason: "throttled" };
    const json = response.headers.get("content-type")?.includes("json");
    if (response.status === success)
      return { ok: true, state: toState((await response.json()) as StateBody) };
    const problem = json
      ? ((await response.json()) as {
          detail?: string;
          errors?: Record<string, string[]>;
        })
      : {};
    if (response.status === 400 && problem.errors)
      return { ok: false, reason: "invalid", errors: problem.errors };
    if (response.status === 404) return { ok: false, reason: "gone" };
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

/** A stored item's create, update or delete: the state, or why not. */
export type StateChangeResult =
  | ActionResult
  | { ok: false; reason: "full" | "missing"; detail?: string };

/**
 * Creates, replaces or deletes one stored item (a routine alarm, a
 * countdown) and answers with the state the server stored. 404 is
 * "missing", 409 at the item's limit is "full".
 */
export async function changeState(
  method: "POST" | "PUT" | "DELETE",
  path: string,
  body?: unknown,
): Promise<StateChangeResult> {
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
    if (response.status === 404) return { ok: false, reason: "missing" };
    const json = response.headers.get("content-type")?.includes("json");
    if (!response.ok) {
      const problem: { detail?: string; errors?: Record<string, string[]> } =
        (json ? await response.json() : null) ?? {};
      if (response.status === 400 && problem.errors)
        return { ok: false, reason: "invalid", errors: problem.errors };
      if (response.status === 409)
        return { ok: false, reason: "full", detail: problem.detail };
      return { ok: false, reason: "refused" };
    }
    if (!json) return { ok: false, reason: "refused" };
    return { ok: true, state: toState((await response.json()) as StateBody) };
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
