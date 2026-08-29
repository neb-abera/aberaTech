/**
 * Rendering times for the person looking at the screen.
 *
 * The server sends instants, always in UTC, and never a preformatted local
 * time. Everything here converts one into the viewer's own zone at the moment
 * it is displayed. That is the whole of the timezone contract on this side: a
 * time is either an instant or it is on screen, and there is no third state in
 * between where somebody's offset could be quietly assumed.
 */

/**
 * The zone the browser thinks it is in.
 *
 * Sent to the server so notifications can be phrased in the same zone the
 * booking page showed, and used here for display. Falls back to UTC only if the
 * browser refuses to say, which no current one does.
 */
export function viewerZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
}

/**
 * "3:40 PM CDT".
 *
 * The zone abbreviation is not decoration. A scheduling tool that shows a bare
 * "3:40 PM" is asking to be read in whichever zone the reader assumes, and the
 * assumption is wrong precisely when it matters, which is when the two people
 * are not in the same place.
 */
export function formatTime(iso: string, zone: string = viewerZone()): string {
  return new Date(iso).toLocaleTimeString("en-US", {
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
    timeZone: zone,
  });
}

/** "Tue, Jun 1" — the heading a group of slots sits under. */
export function formatDay(iso: string, zone: string = viewerZone()): string {
  return new Date(iso).toLocaleDateString("en-US", {
    weekday: "short",
    month: "short",
    day: "numeric",
    timeZone: zone,
  });
}

/** A stable key for the calendar day an instant falls on, in the viewer's zone. */
export function dayKey(iso: string, zone: string = viewerZone()): string {
  return new Date(iso).toLocaleDateString("en-CA", { timeZone: zone });
}

/**
 * Groups slots under the day they fall on for the viewer.
 *
 * Deliberately grouped in the viewer's zone rather than the host's: somebody in
 * Germany booking a late evening slot in Washington should see it under the day
 * it is for them, not under the previous day because that is when it is for
 * somebody else.
 */
export function groupByDay<T extends { startsAt: string }>(
  items: T[],
  zone: string = viewerZone(),
): [string, T[]][] {
  const groups = new Map<string, T[]>();

  for (const item of items) {
    const key = dayKey(item.startsAt, zone);
    const bucket = groups.get(key);
    if (bucket) {
      bucket.push(item);
    } else {
      groups.set(key, [item]);
    }
  }

  return [...groups.entries()];
}

/** "in 25 minutes", "in about 2 hours", "now" — a wait, in words. */
export function describeWait(minutes: number | null): string {
  if (minutes === null) return "not estimated yet";
  if (minutes <= 0) return "now";
  if (minutes === 1) return "in about a minute";
  if (minutes < 60) return `in about ${minutes} minutes`;

  const hours = Math.round(minutes / 60);
  return hours === 1 ? "in about an hour" : `in about ${hours} hours`;
}
