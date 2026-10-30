// Zugriff auf die eigene API: die Summen über Version 1, das Buchungsprotokoll über Version 2.
// Jede Anfrage bekommt eine neue Korrelations-Id; mit ihr findet man in Grafana jede Logzeile
// dieser Anfrage, auch die des Aufrufs bei der Bank.

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

  // Monatsbericht beantragen. Antwort 202: das PDF entsteht danach über analytics.events.
  requestReport(year, month) {
    return this.#send("POST", "v2/analytics/me/reports", { body: { year, month } });
  }

  // Jahresauszug beantragen, eigene Queue analytics.annual-reports.
  requestAnnualReport(year) {
    return this.#send("POST", "v2/analytics/me/annual-reports", { body: { year } });
  }

  reports() {
    return this.#get("v2/analytics/me/reports");
  }

  // Budget und Stand im laufenden Monat; 404, wenn keines festgelegt ist.
  budget() {
    return this.#get("v2/analytics/me/budget");
  }

  setBudget(currency, monthlyLimit) {
    return this.#send("PUT", "v2/analytics/me/budget", { body: { currency, monthlyLimit } });
  }

  deleteBudget() {
    return this.#send("DELETE", "v2/analytics/me/budget");
  }

  report(reportId) {
    return this.#get(`v2/analytics/me/reports/${encodeURIComponent(reportId)}`);
  }

  // Das PDF als Blob; herunterladen muss die Seite selbst, weil die Anfrage ein Token braucht.
  reportDocument(reportId) {
    return this.#send("GET", `v2/analytics/me/reports/${encodeURIComponent(reportId)}/document`, { accept: "application/pdf", blob: true });
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

  #get(path) {
    return this.#send("GET", path);
  }

  async #send(method, path, { body, accept = "application/json", blob = false } = {}) {
    const correlationId = randomUuid();
    const started = performance.now();
    const headers = {
      Accept: accept,
      Authorization: `Bearer ${this.token()}`,
      "X-Correlation-Id": correlationId
    };
    if (body !== undefined) {
      headers["Content-Type"] = "application/json";
    }

    let response;
    try {
      response = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
    } catch {
      this.onRequest({ path, correlationId, status: 0, ms: Math.round(performance.now() - started) });
      throw new ApiError(0, "Der Auswertungsdienst ist nicht erreichbar.", correlationId);
    }

    // X-Data-Source: "live" frisch von der Bank, "stored" der gespeicherte Stand, weil die Bank
    // gerade nicht zu erreichen war.
    this.onRequest({
      path,
      correlationId,
      status: response.status,
      ms: Math.round(performance.now() - started),
      source: response.headers.get("X-Data-Source")
    });

    if (response.ok) {
      if (response.status === 204) {
        return null;
      }
      return blob ? response.blob() : response.json();
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

const CONSENT_TEXT = {
  "statements:read": "Du hast uns nicht erlaubt, deine Buchungen bei der Bank zu lesen.",
  "analytics:read": "Du hast uns nicht erlaubt, deine Auswertungen zu zeigen."
};

export function messageFor(status, problem, missingScope) {
  switch (status) {
    case 400:
    case 404:
    case 409:
      return problem.detail ?? problem.title ?? "Die Eingabe ist ungültig.";
    case 401:
      return "Die Anmeldung ist abgelaufen oder das Token wird nicht akzeptiert.";
    case 403:
      return missingScope
        ? `${CONSENT_TEXT[missingScope] ?? `Du hast uns ${missingScope} nicht freigegeben.`} Melde dich neu an und stimme bei der Bank zu.`
        : "Dafür fehlt die Rolle bank-admin.";
    default:
      return problem.title ?? `Unerwartete Antwort ${status}.`;
  }
}
