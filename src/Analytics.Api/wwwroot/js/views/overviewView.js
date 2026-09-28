import { ApiError } from "../api/analyticsApi.js";
import { currenciesOf, formatCount, formatCompactMoney, formatMoney, monthSeries, yearTotals } from "../domain/figures.js";
import { barChart, card, empty, kpi, loading, notice, select, table } from "./components.js";
import { h, replace } from "./dom.js";

const SCOPE_TEXT = {
  "analytics:read": "Deine Summen bei uns lesen",
  "statements:read": "Deine Kontoauszüge bei der Bank lesen"
};

// Meine Übersicht: Summen eines Jahres je Monat, aus den Buchungen, die die Bank uns meldet.
export function createOverviewView({ api, session, onUnauthorized, onReconsent }) {
  const thisYear = new Date().getFullYear();
  const state = { year: thisYear, currency: null, months: null };

  const controls = h("div", { class: "controls" });
  const body = h("div", { class: "stack" });
  const element = h("div", { class: "view" },
    h("div", { class: "view__head" },
      h("div", {},
        h("h1", {}, "Meine Übersicht"),
        h("p", { class: "lead" }, "Einnahmen und Ausgaben je Monat, aus den Buchungen, die deine Bank uns meldet.")),
      controls),
    body);

  async function load() {
    replace(body, loading("Summen werden geladen …"));
    try {
      state.months = await api.monthly(state.year);
      const currencies = currenciesOf(state.months);
      if (!currencies.includes(state.currency)) {
        state.currency = currencies[0] ?? "CHF";
      }
      render();
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        onUnauthorized();
        return;
      }
      replace(body,
        notice("error", "Summen nicht verfügbar", describe(error)),
        error instanceof ApiError && error.missingScope
          ? h("button", { type: "button", class: "button button--primary", onClick: onReconsent }, "Neu anmelden und zustimmen")
          : null);
    }
  }

  function render() {
    const currencies = currenciesOf(state.months);

    replace(controls,
      select("Jahr", yearOptions(thisYear), state.year, (value) => {
        state.year = Number(value);
        load();
      }),
      currencies.length > 1
        ? select("Währung", currencies.map((code) => ({ value: code, label: code })), state.currency, (value) => {
          state.currency = value;
          render();
        })
        : null);

    replace(body, yearCards(), consentCard(session));
  }

  function yearCards() {
    if (state.months.length === 0) {
      return card(`Jahr ${state.year}`, null,
        empty(`Keine Buchungen in ${state.year}`, "Sobald deine Bank eine Buchung meldet, erscheint sie hier innert Sekunden."));
    }

    const totals = yearTotals(state.months, state.currency);
    const months = monthSeries(state.months, state.currency);
    const money = (value) => formatMoney(value, state.currency);

    return h("div", { class: "stack" },
      h("div", { class: "kpis" },
        kpi("Einnahmen", money(totals.income), { tone: "positive" }),
        kpi("Ausgaben", money(totals.expenses), { tone: "negative" }),
        kpi("Netto", money(totals.net), { tone: totals.net >= 0 ? "positive" : "negative" }),
        kpi("Buchungen", formatCount(totals.transactions), { hint: `im Jahr ${state.year}` })),
      card("Einnahmen und Ausgaben je Monat", `${state.currency}, ${state.year}`,
        barChart({
          labels: months.map((month) => month.label),
          series: [
            { name: "Einnahmen", className: "series-income", values: months.map((month) => month.income) },
            { name: "Ausgaben", className: "series-expenses", values: months.map((month) => month.expenses) }
          ],
          format: formatCompactMoney,
          ariaLabel: `Einnahmen und Ausgaben je Monat ${state.year} in ${state.currency}`
        })),
      card("Monate", "Einnahmen sind Einzahlungen und eingehende Überweisungen, Ausgaben Auszahlungen und ausgehende Überweisungen.",
        table(
          [
            { label: "Monat", render: (row) => row.label },
            { label: "Einnahmen", numeric: true, render: (row) => money(row.income) },
            { label: "Ausgaben", numeric: true, render: (row) => money(row.expenses) },
            { label: "Netto", numeric: true, render: (row) => h("span", { class: row.net < 0 ? "negative" : null }, money(row.net)) },
            { label: "Buchungen", numeric: true, render: (row) => formatCount(row.transactions) }
          ],
          months.map((month) => ({ ...month, muted: month.transactions === 0 })))));
  }

  return { element, load };
}

// Was der Kunde uns bei "Sign in with Bank" erlaubt hat, aus dem Scope seines Tokens. Entziehen
// kann er es nur bei der Bank; wir sehen danach nichts mehr.
function consentCard(session) {
  const granted = session.scopes.filter((scope) => scope in SCOPE_TEXT);
  return card("Deine Freigabe", "Was du uns bei der Anmeldung mit deiner Bank erlaubt hast.",
    h("ul", { class: "grants" },
      granted.map((scope) => h("li", {}, h("code", {}, scope), " ", SCOPE_TEXT[scope])),
      h("li", { class: "grants__never" }, "Nie: Konten, Salden oder Überweisungen bei deiner Bank")),
    h("p", { class: "hint" }, "Entziehen kannst du die Freigabe jederzeit in der App deiner Bank unter „Verbundene Dienste“."));
}

function yearOptions(thisYear) {
  return Array.from({ length: 6 }, (_, index) => thisYear - index).map((year) => ({ value: year, label: String(year) }));
}

export function describe(error) {
  if (error instanceof ApiError) {
    return error.correlationId ? `${error.message} Korrelations-Id: ${error.correlationId}` : error.message;
  }
  return error?.message ?? String(error);
}
