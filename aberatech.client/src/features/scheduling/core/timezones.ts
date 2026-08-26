/**
 * The zones the availability editor lets the host pick from.
 *
 * The browser carries the full IANA list, so nothing here is hand-maintained:
 * a new zone or a renamed one arrives with the next browser update. The one
 * job beyond reading the list is keeping the currently saved zone selectable
 * even if the list does not contain it, so an editor opened on an older
 * browser cannot silently discard a value a newer one wrote.
 */
export function zoneOptions(current?: string): string[] {
  const intl = Intl as typeof Intl & { supportedValuesOf?: (key: 'timeZone') => string[] };

  let zones: string[];
  try {
    zones = intl.supportedValuesOf?.('timeZone') ?? [];
  } catch {
    zones = [];
  }

  if (current && current.length > 0 && !zones.includes(current)) {
    return [current, ...zones];
  }

  return zones;
}
