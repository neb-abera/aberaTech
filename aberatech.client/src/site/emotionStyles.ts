/**
 * The styles a prerendered page carries, gathered into its head.
 *
 * emotion's server render writes a `<style data-emotion="css id">` beside
 * each styled element, 50 to 94 of them a page. The CSP allows an inline
 * style element only by the hash of its text, and a header listing every
 * one of them would run to kilobytes. So the render's elements are taken out
 * of the markup and written back into the head: one element per emotion
 * cache holding every rule in the order the render wrote them, and each
 * global element as it was. The server hashes what the head holds.
 *
 * The browser ends up where it would anyway. emotion's client cache moves
 * every server-rendered style element into the head before React hydrates,
 * and reads the ids in the attribute to know which rules are already there.
 * One element naming all the ids tells it the same.
 */

const STYLE = /<style data-emotion="([^"]*)"[^>]*>([\s\S]*?)<\/style>/g;

export interface HoistedStyles {
  /** The render with its style elements removed. */
  markup: string;
  /** The elements to put in the head: globals first, then one per cache. */
  styles: string;
}

export function hoistStyles(html: string): HoistedStyles {
  const globals = new Map<string, string>();
  const caches = new Map<string, { ids: string[]; css: string[] }>();
  const seen = new Set<string>();

  const markup = html.replace(STYLE, (_, attribute: string, css: string) => {
    const [key, ...ids] = attribute.split(" ");
    if (key.endsWith("-global")) {
      globals.set(
        attribute,
        `<style data-emotion="${attribute}">${css}</style>`,
      );
      return "";
    }
    // A subtree that made its own cache can write a rule a second time.
    const fresh = ids.filter((id) => !seen.has(`${key} ${id}`));
    if (fresh.length === 0) return "";
    for (const id of fresh) seen.add(`${key} ${id}`);
    const cache = caches.get(key) ?? { ids: [], css: [] };
    cache.ids.push(...fresh);
    cache.css.push(css);
    caches.set(key, cache);
    return "";
  });

  const combined = [...caches].map(
    ([key, cache]) =>
      `<style data-emotion="${[key, ...cache.ids].join(" ")}">${cache.css.join("")}</style>`,
  );
  return { markup, styles: [...globals.values(), ...combined].join("") };
}
