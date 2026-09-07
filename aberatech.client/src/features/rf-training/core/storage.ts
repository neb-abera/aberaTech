/**
 * The one door to localStorage for this feature.
 *
 * Three things are kept here, ticks, drill history and gate attempts, and
 * every one of them has the same needs: JSON in, JSON out, and no exception
 * ever reaching the page. Storage can be absent, full, or throw outright in
 * a private window, and none of those should cost the visitor the plan.
 *
 * Nothing here is called during render. The page is prerendered at build
 * time, where there is no window; every caller reads in an effect.
 */

export function readJson<T>(
  key: string,
  guard: (value: unknown) => T | null,
): T | null {
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return null;
    return guard(JSON.parse(raw));
  } catch {
    return null;
  }
}

export function writeJson(key: string, value: unknown): void {
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Storage full or unavailable: the change still shows for this visit.
  }
}
