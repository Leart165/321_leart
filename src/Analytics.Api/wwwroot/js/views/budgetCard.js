import { ApiError } from "../api/analyticsApi.js";
import { barWidth, levelLabel, parseLimit, spentText } from "../domain/budget.js";
import { formatBookedAt } from "../domain/bookings.js";
import { card, empty, notice } from "./components.js";
import { describe } from "./overviewView.js";
import { h, replace } from "./dom.js";

const CURRENCY = "CHF";

// Monatsbudget für Ausgaben. Erreichen die Ausgaben 80 oder 100 Prozent, legt der Dienst im
// Hintergrund eine Warnung an (Queue analytics.budgets); nach dem Speichern fragt die Karte
// deshalb noch zweimal nach.
export function createBudgetCard({ api, onUnauthorized, onChanged = () => {} }) {
  const state = { budget: null, missing: false, input: "", busy: false, error: null, problem: null };

  const body = h("div", { class: "stack" });
  const element = card("Monatsbudget",
    "Wie viel du im Monat höchstens ausgeben willst. Bei 80 und bei 100 Prozent warnen wir dich.",
    body);

  async function load() {
    try {
      state.budget = await api.budget();
      state.missing = false;
      state.input = String(state.budget.monthlyLimit);
      state.error = null;
    } catch (error) {
      if (handleUnauthorized(error)) {
        return;
      }
      if (error instanceof ApiError && error.status === 404) {
        state.budget = null;
        state.missing = true;
      } else {
        state.error = error;
      }
    }
    render();
    onChanged(state.budget);
  }

  async function save() {
    const limit = parseLimit(state.input);
    if (limit === null) {
      state.problem = "Bitte einen Betrag über 0 eingeben, höchstens zwei Nachkommastellen.";
      render();
      return;
    }
    await act(() => api.setBudget(CURRENCY, limit));
    setTimeout(load, 1_500);
    setTimeout(load, 4_000);
  }

  async function remove() {
    await act(() => api.deleteBudget());
  }

  async function act(call) {
    state.busy = true;
    state.problem = null;
    render();
    try {
      await call();
      state.error = null;
    } catch (error) {
      if (handleUnauthorized(error)) {
        return;
      }
      state.error = error;
    } finally {
      state.busy = false;
    }
    await load();
  }

  function handleUnauthorized(error) {
    if (error instanceof ApiError && error.status === 401) {
      onUnauthorized();
      return true;
    }
    return false;
  }

  function render() {
    const input = h("input", {
      id: "budget-limit",
      type: "text",
      inputmode: "decimal",
      value: state.input,
      placeholder: "z. B. 2000",
      onInput: (event) => {
        state.input = event.target.value;
      }
    });

    replace(body,
      h("div", { class: "report-request" },
        h("label", { class: "field", for: "budget-limit" }, h("span", {}, `Budget pro Monat (${CURRENCY})`), input),
        h("button", { type: "button", class: "button button--primary", disabled: state.busy, onClick: save },
          state.budget ? "Ändern" : "Festlegen"),
        state.budget
          ? h("button", { type: "button", class: "button button--ghost", disabled: state.busy, onClick: remove }, "Löschen")
          : null),
      state.problem ? h("p", { class: "form-error", role: "alert" }, state.problem) : null,
      state.error ? notice("error", "Budget nicht verfügbar", describe(state.error)) : null,
      state.budget ? progress(state.budget) : null,
      state.missing && !state.error ? empty("Noch kein Budget", "Betrag eingeben und festlegen.") : null,
      state.budget?.alerts.length
        ? h("ul", { class: "budget-alerts" },
          state.budget.alerts.map((alert) => h("li", { class: `budget-alert budget-alert--${alert.level}` },
            `${levelLabel(alert.level)}: ${alert.spent} ${alert.currency} von ${alert.monthlyLimit}, ${formatBookedAt(alert.raisedAt)}`)))
        : null);
  }

  function progress(budget) {
    return h("div", { class: "budget" },
      h("div", { class: `budget__bar budget__bar--${budget.level}`, role: "progressbar", "aria-valuemin": "0", "aria-valuemax": "100", "aria-valuenow": String(budget.percent) },
        h("span", { style: `width: ${barWidth(budget)}%` })),
      h("p", {}, h("strong", {}, levelLabel(budget.level)), ` · ${spentText(budget)} im laufenden Monat`));
  }

  render();
  return { element, load };
}
