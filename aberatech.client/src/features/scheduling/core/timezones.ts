/**
 * The zones a zone field offers, United States first.
 *
 * The browser carries the full IANA list, so nothing here is hand-maintained
 * beyond the United States zones that lead it: a new zone or a renamed one
 * arrives with the next browser update. The saved zone stays selectable even
 * if the list does not contain it, so an editor opened on an older browser
 * cannot silently discard a value a newer one wrote.
 */

/** Where a zone falls back to when the phone and the browser name none. Neb lives in Washington, DC. */
export const homeZone = "America/New_York";

/**
 * The United States zones, east to west, then Puerto Rico and Guam. Every
 * zone list starts with these, in this order. Neb asked for options to put
 * the United States first on 2026-11-03.
 */
export const usZones: readonly string[] = [
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Phoenix",
  "America/Los_Angeles",
  "America/Anchorage",
  "Pacific/Honolulu",
  "America/Puerto_Rico",
  "Pacific/Guam",
];

/** The zones with the United States ones first, in {@link usZones} order, then the rest in the order given. */
export function usFirst(zones: readonly string[]): string[] {
  const us = new Set(usZones);
  return [...usZones, ...zones.filter((zone) => !us.has(zone))];
}

/** Every zone this browser knows, United States first. */
export function zoneOptions(current?: string): string[] {
  const intl = Intl as typeof Intl & {
    supportedValuesOf?: (key: "timeZone") => string[];
  };

  let zones: string[];
  try {
    zones = intl.supportedValuesOf?.("timeZone") ?? [];
  } catch {
    zones = [];
  }

  const ordered = usFirst(zones);
  if (current && current.length > 0 && !ordered.includes(current)) {
    return [current, ...ordered];
  }

  return ordered;
}
