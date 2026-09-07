/**
 * The owner's saved documents, from the page's side.
 *
 * One GET and one PUT per key. A 401 or 403 is not an error: it is the
 * answer "you are a visitor", and the page renders read-only on it. Only a
 * signed-in owner ever writes, so a visitor's browser never sends a PUT and
 * the database never sees a row it did not ask for.
 */

export type Loaded<T> =
  | { status: "visitor" }
  | { status: "owner"; value: T | null }
  | { status: "error" };

export async function loadDocument<T>(key: string): Promise<Loaded<T>> {
  try {
    const response = await fetch(`/api/progress/${key}`, {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    });
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (response.status === 404) return { status: "owner", value: null };
    if (!response.ok) return { status: "error" };
    // A deployment with no owner answers the SPA shell, not JSON.
    if (!response.headers.get("content-type")?.includes("application/json"))
      return { status: "visitor" };
    return { status: "owner", value: (await response.json()) as T };
  } catch {
    return { status: "error" };
  }
}

/**
 * Save, returning whether the server took it. `keepalive` lets the last save
 * of a page that is closing complete after the page is gone.
 */
export async function saveDocument(
  key: string,
  value: unknown,
  keepalive = false,
): Promise<boolean> {
  try {
    const response = await fetch(`/api/progress/${key}`, {
      method: "PUT",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(value),
      keepalive,
    });
    return response.ok;
  } catch {
    return false;
  }
}
