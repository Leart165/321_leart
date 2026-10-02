// Monatsberichte: reine Funktionen, ohne DOM und ohne Netz.

import { MONTHS } from "./figures.js";

const MONTH_NAMES = [
  "Januar", "Februar", "März", "April", "Mai", "Juni",
  "Juli", "August", "September", "Oktober", "November", "Dezember"
];

export const STATUS = {
  requested: "Wird erstellt …",
  ready: "Bereit",
  failed: "Gescheitert"
};

export function statusLabel(status) {
  return STATUS[status] ?? status;
}

export function isPending(report) {
  return report.status === "requested";
}

export function monthName(year, month) {
  return `${MONTH_NAMES[month - 1] ?? month} ${year}`;
}

// Die letzten zwölf Monate bis zum laufenden, neueste zuerst. Ein Monat in der Zukunft hat
// keine Buchungen, die API lehnt ihn ab.
export function reportMonths(today = new Date(), count = 12) {
  const months = [];
  for (let offset = 0; offset < count; offset++) {
    const date = new Date(Date.UTC(today.getFullYear(), today.getMonth() - offset, 1));
    const year = date.getUTCFullYear();
    const month = date.getUTCMonth() + 1;
    months.push({ value: `${year}-${String(month).padStart(2, "0")}`, label: `${MONTHS[month - 1]} ${year}`, year, month });
  }
  return months;
}

// Standard: der letzte abgeschlossene Monat, den man meistens will.
export function defaultReportMonth(today = new Date()) {
  return reportMonths(today, 2)[1].value;
}

export function parseMonth(value) {
  const match = /^(\d{4})-(\d{2})$/.exec(value ?? "");
  return match ? { year: Number(match[1]), month: Number(match[2]) } : null;
}

export function reportFileName(report) {
  return `buchungen-${report.year}-${String(report.month).padStart(2, "0")}.pdf`;
}

// Ersetzt einen Bericht in der Liste durch seinen neuen Stand oder setzt ihn an den Anfang.
export function mergeReport(reports, updated) {
  const others = reports.filter((report) => report.reportId !== updated.reportId);
  const index = reports.findIndex((report) => report.reportId === updated.reportId);
  if (index < 0) {
    return [updated, ...others];
  }
  const merged = [...reports];
  merged[index] = updated;
  return merged;
}
