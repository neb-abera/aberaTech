/**
 * The booking link a visitor arrived with.
 *
 * A link looks like `/schedule#k=<key>`. The part after `#` never leaves the
 * browser on its own, so the key stays out of the CDN's logs, the server's
 * logs and other sites' Referer headers. The page sends it with each request
 * in a header, and keeps it for the tab's session so a reload keeps the
 * link's rules after the address loses its `#`.
 */

/** The header the server reads the key from (BookingLinkResolver.cs). */
export const linkHeader = "X-Booking-Link";

const storageKey = "aberatech.scheduling.link";

/** The longest key the server looks up. */
const maxLength = 64;

type KeyStore = Pick<Storage, "getItem" | "setItem">;

function plausible(key: string | null): key is string {
  return (
    key !== null && key.length <= maxLength && /^[A-Za-z0-9_-]+$/.test(key)
  );
}

/**
 * The key from the address, or the one this tab kept earlier. A key in the
 * address replaces a kept one, so a visitor sent a second link gets its rules.
 */
export function readLinkKey(
  hash: string,
  store: KeyStore | undefined,
): string | null {
  const fromAddress = new URLSearchParams(hash.replace(/^#/, "")).get("k");
  if (plausible(fromAddress)) {
    store?.setItem(storageKey, fromAddress);
    return fromAddress;
  }

  const kept = store?.getItem(storageKey) ?? null;
  return plausible(kept) ? kept : null;
}

/** The key from this browser, or null on the server renderer. */
export function currentLinkKey(): string | null {
  if (typeof window === "undefined") return null;
  let store: KeyStore | undefined;
  try {
    store = window.sessionStorage;
  } catch {
    // Storage refused, as in some private windows: the address alone still works.
    store = undefined;
  }
  return readLinkKey(window.location.hash, store);
}

/** The headers that carry a key, or none. */
export function linkHeaders(key: string | null): Record<string, string> {
  return key ? { [linkHeader]: key } : {};
}

/** The full link for a key, as the host copies it. */
export function linkUrl(origin: string, key: string): string {
  return `${origin}/schedule#k=${key}`;
}
