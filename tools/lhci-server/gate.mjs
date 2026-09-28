// Who may reach the Lighthouse CI server (docs/lighthouse.md).
//
// Two kinds of caller:
//
//   - The workflows, which upload with the basic auth password. @lhci/cli
//     sends it on every request.
//   - A person in a browser, signed in with Google by the container app's
//     built-in authentication. The platform puts the signed-in user in the
//     X-MS-CLIENT-PRINCIPAL header and strips that header from anything a
//     client sends, so only the platform can set it.
//
// Anyone else gets a redirect to Google sign-in if they asked for a page,
// and 401 otherwise. A signed-in Google account not on the list gets 403.
// /healthz stays open for the probes.
import { Buffer } from "node:buffer";
import { timingSafeEqual } from "node:crypto";

const EMAIL_CLAIMS = new Set([
  "email",
  "emails",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
]);

function same(a, b) {
  const x = Buffer.from(a);
  const y = Buffer.from(b);
  return x.length === y.length && timingSafeEqual(x, y);
}

export function basicAuthOk(header, username, password) {
  if (typeof header !== "string" || !header.startsWith("Basic ")) return false;
  const decoded = Buffer.from(header.slice(6), "base64").toString("utf8");
  const colon = decoded.indexOf(":");
  if (colon < 0) return false;
  // Both compared every time, so the time taken does not say which was wrong.
  const userOk = same(decoded.slice(0, colon), username);
  const passwordOk = same(decoded.slice(colon + 1), password);
  return userOk && passwordOk;
}

// The verified Google address in the principal header, lower case, or "".
export function principalEmail(header) {
  if (typeof header !== "string" || header === "") return "";
  let principal;
  try {
    principal = JSON.parse(Buffer.from(header, "base64").toString("utf8"));
  } catch {
    return "";
  }
  if (principal?.auth_typ !== "google" || !Array.isArray(principal.claims))
    return "";
  const verified = principal.claims.find((c) => c.typ === "email_verified");
  if (verified && String(verified.val).toLowerCase() !== "true") return "";
  const claim = principal.claims.find((c) => EMAIL_CLAIMS.has(c.typ));
  return claim ? String(claim.val).trim().toLowerCase() : "";
}

export function parseAllowed(list) {
  return new Set(
    String(list ?? "")
      .split(/[\s,]+/)
      .map((e) => e.trim().toLowerCase())
      .filter(Boolean),
  );
}

// "allow", "sign-in", "forbidden" or "unauthorized".
export function decide(
  { method, url, headers },
  { username, password, allowed },
) {
  if (url.split("?")[0] === "/healthz") return "allow";
  if (basicAuthOk(headers.authorization, username, password)) return "allow";
  const email = principalEmail(headers["x-ms-client-principal"]);
  if (email) return allowed.has(email) ? "allow" : "forbidden";
  const page =
    (method === "GET" || method === "HEAD") &&
    String(headers.accept ?? "").includes("text/html");
  return page ? "sign-in" : "unauthorized";
}

// Express middleware. No WWW-Authenticate header on a refusal, so a browser
// never shows the password prompt.
export function gate(options) {
  return (req, res, next) => {
    const verdict = decide(req, options);
    if (verdict === "allow") return next();
    if (verdict === "sign-in") {
      res.writeHead(302, {
        location: `/.auth/login/google?post_login_redirect_uri=${encodeURIComponent(req.url)}`,
      });
      return res.end();
    }
    const forbidden = verdict === "forbidden";
    res.writeHead(forbidden ? 403 : 401, { "content-type": "text/plain" });
    return res.end(
      forbidden
        ? "This Google account is not allowed here.\n"
        : "Unauthorized\n",
    );
  };
}
