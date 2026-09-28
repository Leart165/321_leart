// Zugriff auf die eigene API: die Summen über Version 1, das Buchungsprotokoll über Version 2. Jede Anfrage bekommt eine neue Korrelations-Id; mit ihr
// findet man in Grafana jede Logzeile dieser Anfrage.

import { randomUuid } from "../auth/pkce.js";

export class ApiError extends Error {
  constructor(status, message, correlationId, missingScope = null) {
    super(message);
    this.status = status;
    this.correlationId = correlationId;
    this.missingScope = missingScope;
  }
}

export class AnalyticsApi {
  constructor({ token, onRequest = () => {} }) {
    this.token = token;
    this.onRequest = onRequest;
  }

  monthly(year) {
    return this.#get(`v1/analytics/me/monthly?year=${encodeURIComponent(year)}`);
  }

  bookings(from, to, limit) {
    return this.#get(`v2/analytics/me/bookings?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&limit=${encodeURIComponent(limit)}`);
  }

  systemDaily(from, to) {
    return this.#get(`v1/analytics/system/daily?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`);
  }

  async health() {
    try {
      const response = await fetch("health", { headers: { Accept: "application/json" } });
      return await response.json();
    } catch {
      return { status: "Unreachable" };
    }
  }

  async #get(path) {
    const correlationId = randomUuid();
    const started = performance.now();
    let response;

    try {
      response = await fetch(path, {
        headers: {
          Accept: "application/json",
          Authorization: `Bearer ${this.token()}`,
          "X-Correlation-Id": correlationId
        }
      });
    } catch {
      this.onRequest({ path, correlationId, status: 0, ms: Math.round(performance.now() - started) });
      throw new ApiError(0, "Der Auswertungsdienst ist nicht erreichbar.", correlationId);
    }

    this.onRequest({ path, correlationId, status: response.status, ms: Math.round(performance.now() - started) });

    if (response.ok) {
      return response.json();
    }

    const problem = await response.json().catch(() => ({}));
    const missingScope = missingScopeIn(response.headers.get("WWW-Authenticate"));
    throw new ApiError(response.status, messageFor(response.status, problem, missingScope), correlationId, missingScope);
  }
}

// RFC 6750: Bearer error="insufficient_scope", scope="analytics:read"
export function missingScopeIn(challenge) {
  if (!challenge || !challenge.includes("insufficient_scope")) {
    return null;
  }
  const match = challenge.match(/scope="([^"]+)"/);
  return match ? match[1] : null;
}

function messageFor(status, problem, missingScope) {
  switch (status) {
    case 400:
      return problem.detail ?? problem.title ?? "Die Eingabe ist ungültig.";
    case 401:
      return "Die Anmeldung ist abgelaufen oder das Token wird nicht akzeptiert.";
    case 403:
      return missingScope
        ? `Du hast uns ${missingScope} nicht freigegeben. Melde dich neu an und stimme bei der Bank zu.`
        : "Dafür fehlt die Rolle bank-admin.";
    default:
      return problem.title ?? `Unerwartete Antwort ${status}.`;
  }
}
