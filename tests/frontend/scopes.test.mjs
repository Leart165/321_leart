import assert from "node:assert/strict";
import { test } from "node:test";
import { messageFor, missingScopeIn } from "../../src/Analytics.Api/wwwroot/js/api/analyticsApi.js";
import { SCOPE } from "../../src/Analytics.Api/wwwroot/js/auth/keycloak.js";
import { Session } from "../../src/Analytics.Api/wwwroot/js/auth/session.js";

function tokenWith(claims) {
  const part = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${part({ alg: "ES256" })}.${part(claims)}.signatur`;
}

test("Die Scopes kommen durch Leerzeichen getrennt aus dem Token", () => {
  const session = new Session({ accessToken: tokenWith({ sub: "k", scope: "openid analytics:read statements:read" }) });
  assert.deepEqual(session.scopes, ["openid", "analytics:read", "statements:read"]);
  assert.ok(session.hasScope("analytics:read"));
  assert.equal(session.hasScope("accounts:read"), false);
});

test("Ohne Scope-Claim gibt es keine Scopes", () => {
  const session = new Session({ accessToken: tokenWith({ sub: "k" }) });
  assert.deepEqual(session.scopes, []);
});

test("Der fehlende Scope steht nach RFC 6750 im Kopf", () => {
  assert.equal(missingScopeIn('Bearer error="insufficient_scope", scope="analytics:read"'), "analytics:read");
  assert.equal(missingScopeIn('Bearer error="invalid_token"'), null);
  assert.equal(missingScopeIn(null), null);
});

test("Sign in with Bank fragt das Lesen der Buchungen bei der Bank mit an", () => {
  const requested = SCOPE.split(" ");
  assert.ok(requested.includes("analytics:read"));
  assert.ok(requested.includes("statements:read"));
});

test("Fehlt die Zustimmung für die Bank, sagt die Seite das in Worten und bittet um eine neue", () => {
  const message = messageFor(403, {}, "statements:read");
  assert.match(message, /Buchungen bei der Bank/);
  assert.match(message, /stimme bei der Bank zu/);
  assert.equal(messageFor(403, {}, null), "Dafür fehlt die Rolle bank-admin.");
});

test("Ist die Bank weg, zeigt die Seite den Titel der Antwort", () => {
  assert.equal(messageFor(503, { title: "Bank vorübergehend nicht erreichbar" }, null), "Bank vorübergehend nicht erreichbar");
});
