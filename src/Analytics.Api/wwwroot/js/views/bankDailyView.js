import { ApiError } from "../api/analyticsApi.js";
import {
  currenciesOf, currentMonthRange, dailyTotals, daySeries, daysBetween, formatCompactMoney, formatCount, formatDay, formatMoney
} from "../domain/figures.js";
import { barChart, card, dateInput, empty, kpi, loading, notice, select, table } from "./components.js";
import { describe } from "./overviewView.js";
import { h, replace } from "./dom.js";

const MAX_DAYS = 366;

// Tagessummen der ganzen Bank. Nur mit der Rolle bank-admin; die API prüft das selbst, die Seite
// blendet den Reiter nur aus.
export function createBankDailyView({ api, onUnauthorized }) {
  const state = { ...currentMonthRange(), currency: null, days: null };

  const controls = h("div", { class: "controls" });
  const body = h("div", { class: "stack" });
  const element = h("div", { class: "view" },
    h("div", { class: "view__head" },
      h("div", {},
        h("h1", {}, "Bank je Tag"),
        h("p", { class: "lead" }, "Umsatz und Anzahl Buchungen aller Kunden der Bank.")),
      controls),
    body);

  async function load() {
    renderControls();
    if (state.from > state.to) {
      replace(body, notice("warning", "Zeitraum ungültig", "Das Startdatum liegt nach dem Enddatum."));
      return;
    }
    if (daysBetween(state.from, state.to) > MAX_DAYS) {
      replace(body, notice("warning", "Zeitraum zu lang", `Höchstens ${MAX_DAYS} Tage auf einmal.`));
      return;
    }

    replace(body, loading("Tagessummen werden geladen …"));
    try {
      state.days = await api.systemDaily(state.from, state.to);
      const currencies = currenciesOf(state.days);
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
      const title = error instanceof ApiError && error.status === 403 ? "Keine Berechtigung" : "Tagessummen nicht verfügbar";
      replace(body, notice("error", title, describe(error)));
    }
  }

  function renderControls() {
    const currencies = currenciesOf(state.days ?? []);
    replace(controls,
      dateInput("Von", state.from, (value) => {
        state.from = value;
        load();
      }),
      dateInput("Bis", state.to, (value) => {
        state.to = value;
        load();
      }),
      currencies.length > 1
        ? select("Währung", currencies.map((code) => ({ value: code, label: code })), state.currency, (value) => {
          state.currency = value;
          render();
        })
        : null);
  }

  function render() {
    if (state.days.length === 0) {
      replace(body, card("Zeitraum", `${formatDay(state.from)} bis ${formatDay(state.to)}`,
        empty("Keine Buchungen in diesem Zeitraum", "Andere Daten wählen oder bei der Bank eine Buchung auslösen.")));
      return;
    }

    const totals = dailyTotals(state.days, state.currency);
    const series = daySeries(state.days, state.currency, state.from, state.to);
    const money = (value) => formatMoney(value, state.currency);

    replace(body,
      h("div", { class: "kpis" },
        kpi("Volumen", money(totals.volume), { hint: "Ein- und Auszahlungen, ausgehende Überweisungen" }),
        kpi("Einzahlungen", formatCount(totals.deposits)),
        kpi("Auszahlungen", formatCount(totals.withdrawals)),
        kpi("Überweisungen", formatCount(totals.transfers), { hint: "nur die ausgehende Seite gezählt" })),
      card("Volumen je Tag", `${state.currency}, ${formatDay(state.from)} bis ${formatDay(state.to)}`,
        barChart({
          labels: series.map((day) => formatDay(day.day)),
          series: [{ name: "Volumen", className: "series-volume", values: series.map((day) => day.volume) }],
          format: formatCompactMoney,
          labelEvery: Math.max(1, Math.ceil(series.length / 10)),
          ariaLabel: `Volumen je Tag in ${state.currency}`
        })),
      card("Tage mit Buchungen", null,
        table(
          [
            { label: "Tag", render: (row) => formatDay(row.day) },
            { label: "Volumen", numeric: true, render: (row) => money(row.volume) },
            { label: "Einzahlungen", numeric: true, render: (row) => formatCount(row.deposits) },
            { label: "Auszahlungen", numeric: true, render: (row) => formatCount(row.withdrawals) },
            { label: "Überweisungen", numeric: true, render: (row) => formatCount(row.transfers) }
          ],
          series.filter((day) => day.deposits + day.withdrawals + day.transfers > 0))));
  }

  return { element, load };
}
