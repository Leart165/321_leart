// Die Anmeldung des Kunden: Token vom Keycloak der Bank und was die Seite daraus liest.
// Geprüft wird das Token nur von der API; hier wird es nur gelesen, etwa für den Namen.

const STORAGE_KEY = "analytics.session";
const ADMIN_ROLE = "bank-admin";

export function decodeClaims(token) {
  const parts = String(token).split(".");
  if (parts.length !== 3) {
    throw new Error("Das ist kein JWT.");
  }
  const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
  const bytes = Uint8Array.from(atob(padded), (char) => char.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes));
}

export class Session {
  constructor({ accessToken, refreshToken = null, idToken = null, source = "keycloak" }) {
    this.accessToken = accessToken;
    this.refreshToken = refreshToken;
    this.idToken = idToken;
    this.source = source;
    this.claims = decodeClaims(accessToken);
  }

  get ownerId() {
    return this.claims.sub ?? "";
  }

  get displayName() {
    return this.claims.name ?? this.claims.preferred_username ?? this.claims.email ?? this.ownerId;
  }

  get roles() {
    return this.claims.realm_access?.roles ?? [];
  }

  get isAdmin() {
    return this.roles.includes(ADMIN_ROLE);
  }

  get scopes() {
    return String(this.claims.scope ?? "").split(" ").filter(Boolean);
  }

  hasScope(scope) {
    return this.scopes.includes(scope);
  }

  get audiences() {
    const aud = this.claims.aud ?? [];
    return Array.isArray(aud) ? aud : [aud];
  }

  get expiresAt() {
    return (this.claims.exp ?? 0) * 1000;
  }

  secondsLeft(now = Date.now()) {
    return Math.floor((this.expiresAt - now) / 1000);
  }

  isExpired(now = Date.now()) {
    return this.secondsLeft(now) <= 5;
  }

  toJSON() {
    return { accessToken: this.accessToken, refreshToken: this.refreshToken, idToken: this.idToken, source: this.source };
  }
}

// sessionStorage: überlebt ein Neuladen, aber nicht das Schliessen des Tabs.
export function saveSession(session) {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // Ohne Speicher bleibt die Anmeldung bis zum Neuladen im Speicher der Seite.
  }
}

export function loadSession() {
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY);
    return stored ? new Session(JSON.parse(stored)) : null;
  } catch {
    return null;
  }
}

export function clearSession() {
  try {
    sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // nichts zu löschen
  }
}
