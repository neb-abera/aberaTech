// node --test tools/lhci-server/gate.test.mjs, run in the image by the
// Lighthouse job in Checks.
import assert from "node:assert/strict";
import { Buffer } from "node:buffer";
import { test } from "node:test";
import { basicAuthOk, decide, parseAllowed, principalEmail } from "./gate.mjs";

const password = "p".repeat(40);
const options = {
  username: "lhci",
  password,
  allowed: parseAllowed("owner@example.com, Other@Example.com"),
};
const basic = (u, p) => `Basic ${Buffer.from(`${u}:${p}`).toString("base64")}`;
const principal = (claims, auth_typ = "google") =>
  Buffer.from(JSON.stringify({ auth_typ, claims })).toString("base64");
const google = (email, verified = "true") =>
  principal([
    { typ: "email_verified", val: verified },
    {
      typ: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
      val: email,
    },
  ]);
const req = (headers = {}, url = "/app/projects", method = "GET") => ({
  method,
  url,
  headers,
});
const page = { accept: "text/html,application/xhtml+xml" };

test("health check needs nothing", () => {
  assert.equal(decide(req({}, "/healthz"), options), "allow");
});

test("the workflows' basic auth is let through", () => {
  assert.equal(
    decide(
      req({ authorization: basic("lhci", password) }, "/v1/projects", "POST"),
      options,
    ),
    "allow",
  );
});

test("a wrong password or user is refused", () => {
  assert.equal(basicAuthOk(basic("lhci", "wrong"), "lhci", password), false);
  assert.equal(basicAuthOk(basic("admin", password), "lhci", password), false);
  assert.equal(basicAuthOk("Bearer x", "lhci", password), false);
  assert.equal(
    decide(
      req({ authorization: basic("lhci", "wrong") }, "/v1/projects"),
      options,
    ),
    "unauthorized",
  );
});

test("an allowed Google account is let through, case ignored", () => {
  assert.equal(
    decide(
      req({ "x-ms-client-principal": google("OWNER@example.com") }),
      options,
    ),
    "allow",
  );
  assert.equal(
    decide(
      req({ "x-ms-client-principal": google("other@example.com") }),
      options,
    ),
    "allow",
  );
});

test("any other Google account is forbidden", () => {
  assert.equal(
    decide(
      req({ "x-ms-client-principal": google("someone@example.com") }),
      options,
    ),
    "forbidden",
  );
});

test("an unverified address or another provider counts as no sign-in", () => {
  assert.equal(principalEmail(google("owner@example.com", "false")), "");
  assert.equal(
    principalEmail(
      principal([{ typ: "email", val: "owner@example.com" }], "aad"),
    ),
    "",
  );
  assert.equal(principalEmail("not base64 json"), "");
});

test("a browser with no sign-in is sent to Google", () => {
  assert.equal(decide(req(page), options), "sign-in");
  assert.equal(decide(req(page, "/"), options), "sign-in");
});

test("an API call with no sign-in gets 401, not a redirect", () => {
  assert.equal(decide(req({}, "/v1/projects"), options), "unauthorized");
  assert.equal(
    decide(req(page, "/v1/projects", "POST"), options),
    "unauthorized",
  );
});

test("an empty list allows no Google account", () => {
  const none = { ...options, allowed: parseAllowed("") };
  assert.equal(
    decide(req({ "x-ms-client-principal": google("owner@example.com") }), none),
    "forbidden",
  );
});
