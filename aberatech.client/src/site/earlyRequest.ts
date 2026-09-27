/**
 * A page's first API request, started by a script in its head.
 *
 * The page's own fetch runs only after the bundle has loaded and rendered:
 * 810 ms into a warm load of /links from Amman. The head script starts the
 * same request while the document is still parsing, and requestJson hands
 * its answer to the page's first fetch for that address, once. A fetch
 * preload did the same in Chromium and Firefox, but WebKit never gives a
 * preloaded response to fetch(), so Safari asked twice.
 *
 * The script is inline, so the server allows it by hash, read from every
 * shipped page at startup (CspInlineScripts.cs).
 */

declare global {
  // Set by the head script as window.__earlyRequest, which is this.
  var __earlyRequest: { url: string; response: Promise<Response> } | undefined;
}

const init: RequestInit = {
  credentials: "same-origin",
  headers: { Accept: "application/json" },
};

/** The head script for one address. Its promise is caught, so a failure is not logged as uncaught before the page takes it. */
export function earlyRequestScript(url: string): string {
  const address = JSON.stringify(url).replace(/</g, "\\u003c");
  return `window.__earlyRequest={url:${address},response:fetch(${address},${JSON.stringify(init)})};window.__earlyRequest.response.catch(function(){});`;
}

/** A GET for JSON: the head's answer the first time, if it asked for this address. */
export function requestJson(url: string): Promise<Response> {
  const early = globalThis.__earlyRequest;
  if (early?.url === url) {
    globalThis.__earlyRequest = undefined;
    return early.response;
  }
  return fetch(url, init);
}
