import { AnalyticsApi } from "./api/analyticsApi.js";
import { KeycloakLogin, LoginError } from "./auth/keycloak.js";
import { Session, clearSession, loadSession, saveSession } from "./auth/session.js";
import { monthRange } from "./domain/bookings.js";
import { createBankDailyView } from "./views/bankDailyView.js";
import { createBookingsView } from "./views/bookingsView.js";
import { notice } from "./views/components.js";
import { h, replace } from "./views/dom.js";
import { createOverviewView } from "./views/overviewView.js";

const view = document.getElementById("view");
const sessionArea = document.getElementById("session");
const tabs = document.getElementById("tabs");
const statusbar = document.getElementById("statusbar");

const app = { config: null, keycloak: null, session: null, api: null, refreshTimer: null, lastRequest: null };

start();

async function start() {
  try {
    const response = await fetch("frontend/config.json");
    app.config = await response.json();
    app.keycloak = new KeycloakLogin(app.config);
  } catch {
    replace(view, notice("error", "Die Seite konnte nicht starten", "Die Konfiguration unter /frontend/config.json fehlt."));
    return;
  }

  let loginError = null;
  try {
    const fromRedirect = await app.keycloak.completeLogin();
    app.session = fromRedirect ?? loadSession();
  } catch (error) {
    loginError = error;
  }

  if (app.session?.isExpired()) {
    app.session = await app.keycloak.refresh(app.session);
  }

  refreshHealth();
  setInterval(refreshHealth, 15_000);
  window.addEventListener("hashchange", route);

  if (!app.session) {
    showSignedOut(loginError);
    return;
  }

  signIn(app.session);
}

function signIn(session) {
  app.session = session;
  saveSession(session);
  app.api = new AnalyticsApi({ token: () => app.session.accessToken, onRequest: rememberRequest });
  scheduleRefresh();
  renderSession();
  renderTabs();
  route();
}

function signOut() {
  const session = app.session;
  clearSession();
  app.session = null;
  clearTimeout(app.refreshTimer);
  if (session?.source === "keycloak") {
    app.keycloak.logout(session);
  } else {
    showSignedOut(null);
  }
}

// Kurz vor Ablauf still erneuern; ein eingefügtes Token hat kein Refresh-Token und läuft einfach ab.
function scheduleRefresh() {
  clearTimeout(app.refreshTimer);
  const delay = Math.max(5, app.session.secondsLeft() - 30) * 1000;
  app.refreshTimer = setTimeout(async () => {
    const renewed = await app.keycloak.refresh(app.session);
    if (renewed) {
      signIn(renewed);
    } else {
      expired();
    }
  }, delay);
}

// Fehlt der Scope, hilft nur eine neue Anmeldung, bei der die Bank den Kunden wieder fragt.
function reconsent() {
  clearSession();
  app.session = null;
  app.keycloak.login().catch((failure) => showSignedOut(failure));
}

function expired() {
  clearSession();
  app.session = null;
  showSignedOut(new LoginError("Die Anmeldung ist abgelaufen. Bitte erneut anmelden."));
}

function route() {
  if (!app.session) {
    return;
  }
  const [path, query] = location.hash.slice(1).split("?");
  const wanted = path === "/bank" && app.session.isAdmin ? "bank" : path === "/bookings" ? "bookings" : "overview";
  for (const button of tabs.querySelectorAll("[data-view]")) {
    button.setAttribute("aria-selected", String(button.dataset.view === wanted));
  }

  const screen = createScreen(wanted, new URLSearchParams(query ?? ""));
  replace(view, screen.element);
  screen.load();
}

function createScreen(wanted, parameters) {
  switch (wanted) {
    case "bank":
      return createBankDailyView({ api: app.api, onUnauthorized: expired });
    case "bookings":
      return createBookingsView({ api: app.api, range: rangeFrom(parameters), onUnauthorized: expired, onReconsent: reconsent });
    default:
      return createOverviewView({ api: app.api, session: app.session, onUnauthorized: expired, onReconsent: reconsent });
  }
}

// #/bookings?month=2026-09 kommt aus der Monatstabelle der Übersicht.
function rangeFrom(parameters) {
  const month = parameters.get("month")?.match(/^(\d{4})-(\d{2})$/);
  return month ? monthRange(Number(month[1]), Number(month[2])) : null;
}

function renderTabs() {
  replace(tabs,
    h("a", { href: "#/overview", role: "tab", dataset: { view: "overview" } }, "Meine Übersicht"),
    h("a", { href: "#/bookings", role: "tab", dataset: { view: "bookings" } }, "Buchungen"),
    app.session.isAdmin ? h("a", { href: "#/bank", role: "tab", dataset: { view: "bank" } }, "Bank je Tag") : null);
}

function renderSession() {
  const session = app.session;
  replace(sessionArea,
    h("div", { class: "who" },
      h("span", { class: "avatar", "aria-hidden": "true" }, initials(session.displayName)),
      h("span", { class: "who__text" },
        h("strong", {}, session.displayName),
        h("span", {}, session.isAdmin ? "Kunde · bank-admin" : "Kunde der Bank"))),
    h("button", { type: "button", class: "button button--ghost", onClick: signOut }, "Abmelden"));
}

function showSignedOut(error) {
  replace(tabs);
  replace(sessionArea);

  const tokenField = h("textarea", { id: "token", rows: 4, spellcheck: "false", placeholder: "eyJhbGciOiJFUzI1NiIs…" });
  const pasteError = h("p", { class: "form-error", role: "alert" });

  replace(view,
    h("div", { class: "signin" },
      h("section", { class: "signin__card" },
        h("h1", {}, "Auswertungen zu deinen Konten"),
        h("p", { class: "lead" },
          "Wir rechnen für deine Bank Statistiken zu deinen Buchungen. Melde dich mit dem Login deiner Bank an; " +
          "ein eigenes Konto bei uns brauchst du nicht. Deine Bank fragt dich beim ersten Mal, ob du uns das Lesen deiner Summen und Buchungen erlaubst."),
        error ? notice("error", error.message, error.hint) : null,
        h("button", {
          type: "button",
          class: "button button--primary",
          onClick: () => app.keycloak.login().catch((failure) => showSignedOut(failure))
        }, "Sign in with Bank"),
        h("details", { class: "paste" },
          h("summary", {}, "Entwicklung: Access Token einfügen"),
          h("p", { class: "hint" }, "Das Token braucht die Audience m321-analytics-api und den Scope analytics:read."),
          h("label", { for: "token", class: "sr-only" }, "Access Token"),
          tokenField,
          pasteError,
          h("button", {
            type: "button",
            class: "button",
            onClick: () => {
              try {
                const session = new Session({ accessToken: tokenField.value.trim(), source: "pasted" });
                if (session.isExpired()) {
                  pasteError.textContent = "Dieses Token ist bereits abgelaufen.";
                  return;
                }
                signIn(session);
              } catch {
                pasteError.textContent = "Das ist kein gültiges JWT.";
              }
            }
          }, "Mit diesem Token weiter")))));
}

function rememberRequest(request) {
  app.lastRequest = request;
  renderStatus();
}

let health = { status: "…" };

async function refreshHealth() {
  health = await new AnalyticsApi({ token: () => "" }).health();
  renderStatus();
}

function renderStatus() {
  const request = app.lastRequest;
  replace(statusbar,
    h("span", { class: `dot dot--${String(health.status).toLowerCase()}`, "aria-hidden": "true" }),
    h("span", {}, `Dienst: ${health.status}`),
    request
      ? h("span", { class: "statusbar__request" },
        `${request.path.split("?")[0]} · ${request.status || "keine Antwort"} · ${request.ms} ms · Korrelations-Id `,
        h("button", {
          type: "button",
          class: "link",
          title: "In die Zwischenablage kopieren, um sie in Grafana zu suchen",
          onClick: () => navigator.clipboard?.writeText(request.correlationId)
        }, request.correlationId))
      : null,
    h("a", { href: "swagger", class: "statusbar__link" }, "API (Swagger)"));
}

function initials(name) {
  return String(name).split(/[\s.@_-]+/).filter(Boolean).slice(0, 2).map((part) => part[0].toUpperCase()).join("") || "?";
}
