import { ApiError } from "../api/analyticsApi.js";
import {
  KIND_FILTERS, bookingTotals, filterBookings, formatBookedAt, isIncoming, kindLabel, lastDaysRange, signedAmount
} from "../domain/bookings.js";
import { currenciesOf, daysBetween, formatCount, formatDay, formatMoney } from "../domain/figures.js";
import { card, dateInput, empty, kpi, loading, notice, select, table } from "./components.js";
import { createReportsCard } from "./reportsCard.js";
import { describe } from "./overviewView.js";
import { h, replace } from "./dom.js";

const DEFAULT_DAYS = 30;
const MAX_DAYS = 366;
const LIMIT = 500;

// Buchungen: jede einzelne Buchung im Zeitraum, neueste zuerst, wie ein Log.
export function createBookingsView({ api, range, onUnauthorized, onReconsent }) {
  const state = { ...(range ?? lastDaysRange(DEFAULT_DAYS)), kind: "all", currency: null, bookings: null };

  const controls = h("div", { class: "controls" });
  const body = h("div", { class: "stack" });
  const reports = createReportsCard({ api, onUnauthorized });
  const element = h("div", { class: "view" },
    h("div", { class: "view__head" },
      h("div", {},
        h("h1", {}, "Buchungen"),
        h("p", { class: "lead" }, "Jede Buchung, die deine Bank uns gemeldet hat, einzeln und neueste zuerst.")),
      controls),
    body,
    reports.element);

  async function load() {
    reports.load();
    renderControls();
    if (state.from > state.to) {
      replace(body, notice("warning", "Zeitraum ungültig", "Das Startdatum liegt nach dem Enddatum."));
      return;
    }
    if (daysBetween(state.from, state.to) > MAX_DAYS) {
      replace(body, notice("warning", "Zeitraum zu lang", `Höchstens ${MAX_DAYS} Tage auf einmal.`));
      return;
    }

    replace(body, loading("Buchungen werden geladen …"));
    try {
      state.bookings = await api.bookings(state.from, state.to, LIMIT);
      const currencies = currenciesOf(state.bookings);
      if (!currencies.includes(state.currency)) {
        state.currency = currencies[0] ?? "CHF";
      }
      renderControls();
      render();
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        onUnauthorized();
        return;
      }
      replace(body,
        notice("error", "Buchungen nicht verfügbar", describe(error)),
        error instanceof ApiError && error.missingScope
          ? h("button", { type: "button", class: "button button--primary", onClick: onReconsent }, "Neu anmelden und zustimmen")
          : null);
    }
  }

  function renderControls() {
    const currencies = currenciesOf(state.bookings ?? []);
    replace(controls,
      dateInput("Von", state.from, (value) => {
        state.from = value;
        load();
      }),
      dateInput("Bis", state.to, (value) => {
        state.to = value;
        load();
      }),
      select("Art", KIND_FILTERS, state.kind, (value) => {
        state.kind = value;
        render();
      }),
      currencies.length > 1
        ? select("Währung", currencies.map((code) => ({ value: code, label: code })), state.currency, (value) => {
          state.currency = value;
          render();
        })
        : null);
  }

  function render() {
    const period = `${formatDay(state.from)}.${state.from.slice(0, 4)} bis ${formatDay(state.to)}.${state.to.slice(0, 4)}`;

    if (state.bookings.length === 0) {
      replace(body, card("Protokoll", period,
        empty("Keine Buchungen in diesem Zeitraum", "Sobald deine Bank eine Buchung meldet, erscheint sie hier innert Sekunden.")));
      return;
    }

    const shown = filterBookings(state.bookings, { kind: state.kind, currency: state.currency });
    const totals = bookingTotals(shown);
    const money = (value) => formatMoney(value, state.currency);

    replace(body,
      h("div", { class: "kpis" },
        kpi("Buchungen", formatCount(totals.count), { hint: "mit dem gewählten Filter" }),
        kpi("Einnahmen", money(totals.income), { tone: "positive" }),
        kpi("Ausgaben", money(totals.expenses), { tone: "negative" }),
        kpi("Netto", money(totals.net), { tone: totals.net >= 0 ? "positive" : "negative" })),
      state.bookings.length >= LIMIT
        ? notice("warning", `Nur die neuesten ${LIMIT} Buchungen`, "Für ältere Buchungen einen kürzeren Zeitraum wählen.")
        : null,
      card("Protokoll", `${period}, ${state.currency}`,
        shown.length === 0
          ? empty("Keine Buchung passt zum Filter", "Eine andere Art wählen.")
          : table(
            [
              { label: "Zeitpunkt", render: (row) => h("time", { datetime: row.bookedAt }, formatBookedAt(row.bookedAt)) },
              { label: "Art", render: (row) => h("span", { class: `kind kind--${isIncoming(row.kind) ? "in" : "out"}` }, kindLabel(row.kind)) },
              {
                label: "Betrag",
                numeric: true,
                render: (row) => h("span", { class: isIncoming(row.kind) ? "positive" : "negative" }, signedMoney(signedAmount(row), row.currency))
              },
              { label: "Transaktion", render: (row) => h("code", { class: "txid", title: row.transactionId }, row.transactionId.slice(0, 8)) }
            ],
            shown)));
  }

  return { element, load };
}

function signedMoney(amount, currency) {
  const text = formatMoney(Math.abs(amount), currency);
  return amount < 0 ? `− ${text}` : `+ ${text}`;
}
