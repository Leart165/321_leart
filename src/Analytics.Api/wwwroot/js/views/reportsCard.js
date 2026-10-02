import { ApiError } from "../api/analyticsApi.js";
import { defaultReportMonth, isPending, mergeReport, monthName, parseMonth, reportFileName, reportMonths, statusLabel } from "../domain/reports.js";
import { formatBookedAt } from "../domain/bookings.js";
import { card, empty, notice, select, table } from "./components.js";
import { describe } from "./overviewView.js";
import { h, replace } from "./dom.js";

const POLL_MS = 1_000;
const POLL_LIMIT = 60;

// Monatsbericht als PDF: beantragen, warten, herunterladen. Die API antwortet auf den Antrag
// sofort mit 202; das PDF entsteht im Hintergrund über den Exchange analytics.events. Solange ein
// Bericht "Wird erstellt" ist, fragt die Seite jede Sekunde nach.
export function createReportsCard({ api, onUnauthorized }) {
  const state = { month: defaultReportMonth(), reports: [], busy: false, error: null, polls: 0, timer: null };

  const body = h("div", { class: "stack" });
  const element = card("Monatsbericht als PDF",
    "Jede Buchung eines Monats und die Summen, zum Ablegen oder Ausdrucken. Der Bericht wird im Hintergrund erstellt.",
    body);

  async function load() {
    try {
      state.reports = await api.reports();
      state.error = null;
    } catch (error) {
      if (handleUnauthorized(error)) {
        return;
      }
      state.error = error;
    }
    render();
    schedulePoll();
  }

  async function request() {
    const wanted = parseMonth(state.month);
    if (!wanted || state.busy) {
      return;
    }
    state.busy = true;
    state.error = null;
    render();
    try {
      const report = await api.requestReport(wanted.year, wanted.month);
      state.reports = mergeReport(state.reports, report);
      state.polls = 0;
    } catch (error) {
      if (handleUnauthorized(error)) {
        return;
      }
      state.error = error;
    } finally {
      state.busy = false;
    }
    render();
    schedulePoll();
  }

  function schedulePoll() {
    clearTimeout(state.timer);
    const pending = state.reports.filter(isPending);
    if (pending.length === 0 || state.polls >= POLL_LIMIT || !element.isConnected) {
      return;
    }
    state.timer = setTimeout(async () => {
      state.polls += 1;
      for (const report of pending) {
        try {
          state.reports = mergeReport(state.reports, await api.report(report.reportId));
        } catch (error) {
          if (handleUnauthorized(error)) {
            return;
          }
        }
      }
      render();
      schedulePoll();
    }, POLL_MS);
  }

  async function download(report) {
    try {
      const pdf = await api.reportDocument(report.reportId);
      const url = URL.createObjectURL(pdf);
      const link = h("a", { href: url, download: reportFileName(report) });
      document.body.append(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 10_000);
    } catch (error) {
      if (!handleUnauthorized(error)) {
        state.error = error;
        render();
      }
    }
  }

  function handleUnauthorized(error) {
    if (error instanceof ApiError && error.status === 401) {
      clearTimeout(state.timer);
      onUnauthorized();
      return true;
    }
    return false;
  }

  function render() {
    replace(body,
      h("div", { class: "report-request" },
        select("Monat", reportMonths(), state.month, (value) => {
          state.month = value;
        }),
        h("button", { type: "button", class: "button button--primary", disabled: state.busy, onClick: request },
          state.busy ? "Wird beantragt …" : "Monatsbericht anfordern")),
      state.error ? notice("error", "Monatsbericht nicht möglich", describe(state.error)) : null,
      state.polls >= POLL_LIMIT && state.reports.some(isPending)
        ? notice("warning", "Dauert länger als erwartet", "Der Bericht wird erstellt, sobald der Dienst wieder Anträge verarbeitet. Seite später neu laden.")
        : null,
      state.reports.length === 0
        ? empty("Noch kein Monatsbericht", "Monat wählen und anfordern.")
        : table(
          [
            { label: "Monat", render: (row) => monthName(row.year, row.month) },
            { label: "Beantragt", render: (row) => formatBookedAt(row.requestedAt) },
            { label: "Status", render: (row) => statusCell(row) },
            { label: "Buchungen", numeric: true, render: (row) => (row.bookingCount ?? "–") },
            {
              label: "PDF",
              render: (row) => row.status === "ready"
                ? h("button", { type: "button", class: "button", onClick: () => download(row) }, "Herunterladen")
                : "–"
            }
          ],
          state.reports));
  }

  function statusCell(report) {
    return h("span", { class: `report-status report-status--${report.status}`, title: report.failure ?? null },
      isPending(report) ? h("span", { class: "spinner", "aria-hidden": "true" }) : null,
      statusLabel(report.status));
  }

  render();
  return { element, load };
}
