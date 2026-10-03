/**
 * Ring in this browser: for a computer where nothing can be installed.
 * Plain fetch, Web Audio and the Notification API, nothing else. It rings
 * only while the tab is open.
 */

import type { AlertItem, AlertsState } from "./api";

/** Where the switch is kept. Per browser: each computer decides for itself. */
export const ringPreferenceKey = "abera.alerts.ringInBrowser";

/** How often the page asks the server while the switch is on. */
export const ringPollMs = 15_000;

export function readRingPreference(): boolean {
  try {
    return window.localStorage.getItem(ringPreferenceKey) === "on";
  } catch {
    return false;
  }
}

export function writeRingPreference(on: boolean): void {
  try {
    if (on) window.localStorage.setItem(ringPreferenceKey, "on");
    else window.localStorage.removeItem(ringPreferenceKey);
  } catch {
    // Storage blocked: the switch still works until the tab closes.
  }
}

/**
 * The alarm to ring for now: an alarm whose time has come and whose event
 * has not started, not skipped, not muted and not acknowledged anywhere.
 * The earliest, when two overlap.
 */
export function dueAlarm(alerts: AlertItem[], now: number): AlertItem | null {
  let due: AlertItem | null = null;
  for (const alert of alerts) {
    if (alert.type !== "alarm" || alert.skipped || alert.muted) continue;
    if (alert.acknowledged) continue;
    const at = Date.parse(alert.alertAt);
    if (!(at <= now && now < Date.parse(alert.startsAt))) continue;
    if (due === null || at < Date.parse(due.alertAt)) due = alert;
  }
  return due;
}

/** What rings in the browser: a calendar alarm or a routine alarm's ring. */
export interface Ringing {
  key: string;
  title: string;
  alertAt: string;
  startsAt: string;
  /** A routine alarm: startsAt is when it stops, not an event's start. */
  routine: boolean;
}

/**
 * What to ring for now, from the calendar alarms and the routine alarms'
 * rings together: the earliest due item that is not acknowledged anywhere.
 * A ring is due from its time until it stops, and a mute silences a ring
 * whose time falls inside it, as it does an event's alarm.
 */
export function dueRinging(
  state: Pick<AlertsState, "alerts" | "routineRings" | "mutedUntil">,
  now: number,
): Ringing | null {
  const alarm = dueAlarm(state.alerts, now);
  let due: Ringing | null = alarm && {
    key: alarm.key,
    title: alarm.title,
    alertAt: alarm.alertAt,
    startsAt: alarm.startsAt,
    routine: false,
  };
  const mutedUntil = state.mutedUntil ? Date.parse(state.mutedUntil) : null;
  for (const ring of state.routineRings ?? []) {
    if (ring.acknowledged) continue;
    const at = Date.parse(ring.alertAt);
    if (!(at <= now && now < Date.parse(ring.startsAt))) continue;
    if (mutedUntil !== null && mutedUntil > now && at < mutedUntil) continue;
    if (due === null || at < Date.parse(due.alertAt))
      due = {
        key: ring.key,
        title: ring.label,
        alertAt: ring.alertAt,
        startsAt: ring.startsAt,
        routine: true,
      };
  }
  return due;
}

/** A tone that can be started and stopped. */
export interface Tone {
  /** Called from the switch's click: browsers let audio start only after a gesture. */
  unlock(): void;
  start(): void;
  stop(): void;
}

/**
 * A looping two-note beep made with Web Audio, so no audio file is fetched:
 * 0.25 s at 880 Hz, 0.25 s at 660 Hz, then 0.5 s of quiet, every second.
 */
export function webAudioTone(): Tone {
  let context: AudioContext | null = null;
  let timer = 0;

  const beep = () => {
    if (!context) return;
    const start = context.currentTime;
    [880, 660].forEach((frequency, index) => {
      if (!context) return;
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.type = "square";
      oscillator.frequency.value = frequency;
      gain.gain.value = 0.2;
      oscillator.connect(gain).connect(context.destination);
      oscillator.start(start + index * 0.25);
      oscillator.stop(start + index * 0.25 + 0.25);
    });
  };

  return {
    unlock() {
      try {
        const Context =
          window.AudioContext ??
          (window as unknown as { webkitAudioContext?: typeof AudioContext })
            .webkitAudioContext;
        if (!context && Context) context = new Context();
        void context?.resume();
      } catch {
        context = null;
      }
    },
    start() {
      if (timer) return;
      try {
        void context?.resume();
        beep();
      } catch {
        // No audio: the title and the banner still ring.
      }
      timer = window.setInterval(() => {
        try {
          beep();
        } catch {
          // As above.
        }
      }, 1000);
    },
    stop() {
      window.clearInterval(timer);
      timer = 0;
    },
  };
}

/** A system notification, when the browser has one and the owner allowed it. */
export function notify(title: string, body: string, tag: string): void {
  try {
    if (!("Notification" in window)) return;
    if (Notification.permission !== "granted") return;
    new Notification(title, { body, tag, requireInteraction: true });
  } catch {
    // Some browsers allow notifications only from a service worker.
  }
}

/** Asks once, from the switch's click. The ring works without it. */
export function askToNotify(): void {
  try {
    if (!("Notification" in window)) return;
    if (Notification.permission !== "default") return;
    void Notification.requestPermission().catch(() => undefined);
  } catch {
    // An older browser with the callback form, or none.
  }
}
