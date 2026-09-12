/**
 * The document API's answers, faked for tests: a status, an optional JSON
 * body, and the content-type header the page's loader checks. One helper
 * so the four suites that fake this API cannot drift apart on its shape.
 */
export const respond = (status: number, body: unknown = null) => ({
  status,
  ok: status >= 200 && status < 300,
  headers: {
    get: (name: string) =>
      name === "content-type" ? "application/json" : null,
  },
  json: async () => body,
});
