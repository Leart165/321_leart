// Login beim Keycloak der Bank: Authorization Code mit PKCE, ohne Bibliothek.
// Die Seite hat keine eigenen Benutzer; wer sich anmeldet, ist Kunde der Bank.

import { codeChallenge, randomString } from "./pkce.js";
import { Session } from "./session.js";

const PENDING_KEY = "analytics.login";

// analytics:read ist der Standard-Scope von analytics-web; ausdrücklich angefragt, damit die
// Zustimmung des Kunden genau das nennt, was die Seite braucht.
const SCOPE = "openid analytics:read";

export class LoginError extends Error {
  constructor(message, hint) {
    super(message);
    this.hint = hint;
  }
}

export class KeycloakLogin {
  constructor({ issuer, clientId }) {
    this.issuer = issuer.replace(/\/$/, "");
    this.clientId = clientId;
    this.redirectUri = `${location.origin}${location.pathname}`;
    this.metadata = null;
  }

  async discover() {
    if (this.metadata) {
      return this.metadata;
    }
    let response;
    try {
      response = await fetch(`${this.issuer}/.well-known/openid-configuration`);
    } catch {
      throw new LoginError("Der Keycloak der Bank ist nicht erreichbar.", this.issuer);
    }
    if (!response.ok) {
      throw new LoginError(`Der Keycloak der Bank antwortet mit ${response.status}.`, this.issuer);
    }
    this.metadata = await response.json();
    return this.metadata;
  }

  async login() {
    const metadata = await this.discover();
    const verifier = randomString(64);
    const state = randomString(32);
    sessionStorage.setItem(PENDING_KEY, JSON.stringify({ verifier, state }));

    const url = new URL(metadata.authorization_endpoint);
    url.search = new URLSearchParams({
      response_type: "code",
      client_id: this.clientId,
      redirect_uri: this.redirectUri,
      scope: SCOPE,
      state,
      code_challenge: await codeChallenge(verifier),
      code_challenge_method: "S256"
    }).toString();

    location.assign(url.toString());
  }

  // Nach der Rückkehr vom Keycloak: Code gegen Token tauschen. null, wenn gar kein Login lief.
  async completeLogin() {
    const params = new URLSearchParams(location.search);
    if (!params.has("code") && !params.has("error")) {
      return null;
    }

    history.replaceState(null, "", this.redirectUri + location.hash);

    if (params.has("error")) {
      throw new LoginError(
        `Anmeldung abgelehnt: ${params.get("error_description") ?? params.get("error")}`,
        `Im Keycloak braucht der Client ${this.clientId} die Redirect-URI ${this.redirectUri}*`);
    }

    const pending = JSON.parse(sessionStorage.getItem(PENDING_KEY) ?? "null");
    sessionStorage.removeItem(PENDING_KEY);
    if (!pending || pending.state !== params.get("state")) {
      throw new LoginError("Die Antwort vom Keycloak passt nicht zu diesem Login. Bitte erneut anmelden.");
    }

    return this.#requestTokens({
      grant_type: "authorization_code",
      code: params.get("code"),
      redirect_uri: this.redirectUri,
      code_verifier: pending.verifier
    });
  }

  async refresh(session) {
    if (!session.refreshToken) {
      return null;
    }
    try {
      return await this.#requestTokens({ grant_type: "refresh_token", refresh_token: session.refreshToken });
    } catch {
      return null;
    }
  }

  async logout(session) {
    const metadata = await this.discover().catch(() => null);
    if (!metadata?.end_session_endpoint || !session?.idToken) {
      location.assign(this.redirectUri);
      return;
    }
    const url = new URL(metadata.end_session_endpoint);
    url.search = new URLSearchParams({
      client_id: this.clientId,
      id_token_hint: session.idToken,
      post_logout_redirect_uri: this.redirectUri
    }).toString();
    location.assign(url.toString());
  }

  async #requestTokens(form) {
    const metadata = await this.discover();
    let response;
    try {
      response = await fetch(metadata.token_endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({ client_id: this.clientId, ...form })
      });
    } catch {
      throw new LoginError(
        "Das Token liess sich nicht abholen.",
        `Im Keycloak braucht der Client ${this.clientId} "Web Origins" mit ${location.origin}.`);
    }

    const body = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new LoginError(`Keycloak lehnt ab: ${body.error_description ?? body.error ?? response.status}`);
    }

    return new Session({
      accessToken: body.access_token,
      refreshToken: body.refresh_token ?? null,
      idToken: body.id_token ?? null
    });
  }
}
