import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { test } from "node:test";
import { base64Url, codeChallenge, randomString, randomUuid, sha256 } from "../../src/Analytics.Api/wwwroot/js/auth/pkce.js";
import { Session, decodeClaims } from "../../src/Analytics.Api/wwwroot/js/auth/session.js";

function tokenWith(claims) {
  const part = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${part({ alg: "ES256" })}.${part(claims)}.signatur`;
}

test("Die Challenge entspricht dem Beispiel aus RFC 7636", async () => {
  assert.equal(
    await codeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"),
    "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
});

test("Die eigene SHA-256 rechnet wie die von Node, auch über mehrere Blöcke", () => {
  for (const text of ["", "abc", "ä".repeat(40), "x".repeat(1000)]) {
    const expected = createHash("sha256").update(text).digest("base64url");
    assert.equal(base64Url(sha256(new TextEncoder().encode(text))), expected, `für ${text.length} Zeichen`);
  }
});

test("Zufallswerte haben das verlangte Format", () => {
  assert.match(randomString(64), /^[A-Za-z0-9\-._~]{64}$/);
  assert.match(randomUuid(), /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
});

test("Name, Rollen und Audience kommen aus dem Token", () => {
  const session = new Session({
    accessToken: tokenWith({
      sub: "kunde-1",
      name: "Lea Muster",
      aud: ["m321-analytics-api", "m321-accounts-api"],
      realm_access: { roles: ["bank-admin"] },
      exp: Math.floor(Date.now() / 1000) + 300
    })
  });

  assert.equal(session.displayName, "Lea Muster");
  assert.equal(session.ownerId, "kunde-1");
  assert.ok(session.isAdmin);
  assert.deepEqual(session.audiences, ["m321-analytics-api", "m321-accounts-api"]);
  assert.equal(session.isExpired(), false);
});

test("Ohne Rolle kein Admin, und ein abgelaufenes Token gilt als abgelaufen", () => {
  const session = new Session({ accessToken: tokenWith({ sub: "kunde-2", aud: "m321-analytics-api", exp: 1 }) });
  assert.equal(session.isAdmin, false);
  assert.deepEqual(session.audiences, ["m321-analytics-api"]);
  assert.ok(session.isExpired());
});

test("Umlaute im Token werden richtig gelesen", () => {
  assert.equal(decodeClaims(tokenWith({ name: "Jürg Müller" })).name, "Jürg Müller");
});

test("Etwas, das kein JWT ist, wird abgelehnt", () => {
  assert.throws(() => decodeClaims("kein-token"));
});
