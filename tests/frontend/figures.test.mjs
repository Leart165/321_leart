import assert from "node:assert/strict";
import { test } from "node:test";
import {
  currenciesOf, currentMonthRange, dailyTotals, daySeries, daysBetween, formatCompactMoney, monthSeries, yearTotals
} from "../../src/Analytics.Api/wwwroot/js/domain/figures.js";

const months = [
  { year: 2026, month: 1, currency: "CHF", income: 0.1, expenses: 0, net: 0.1, transactions: 1 },
  { year: 2026, month: 2, currency: "CHF", income: 0.2, expenses: 0.05, net: 0.15, transactions: 2 },
  { year: 2026, month: 2, currency: "EUR", income: 99, expenses: 1, net: 98, transactions: 1 }
];

test("Jahressummen rechnen in Rappen, ohne Gleitkommafehler", () => {
  const totals = yearTotals(months, "CHF");
  assert.equal(totals.income, 0.3);
  assert.equal(totals.expenses, 0.05);
  assert.equal(totals.net, 0.25);
  assert.equal(totals.transactions, 3);
});

test("Jahressummen zählen nur die gewählte Währung", () => {
  assert.equal(yearTotals(months, "EUR").income, 99);
});

test("Die Monatsreihe hat immer zwölf Monate, auch ohne Buchung", () => {
  const series = monthSeries(months, "CHF");
  assert.equal(series.length, 12);
  assert.equal(series[1].income, 0.2);
  assert.equal(series[11].transactions, 0);
});

test("Währungen aus mehreren Listen, eindeutig und sortiert", () => {
  assert.deepEqual(currenciesOf(months, [{ currency: "AUD" }]), ["AUD", "CHF", "EUR"]);
});

test("Die Tagesreihe füllt Tage ohne Buchung mit null", () => {
  const days = [{ day: "2026-09-02", currency: "CHF", volume: 10, deposits: 1, withdrawals: 0, transfers: 0 }];
  const series = daySeries(days, "CHF", "2026-09-01", "2026-09-03");
  assert.deepEqual(series.map((day) => day.volume), [0, 10, 0]);
  assert.equal(daysBetween("2026-09-01", "2026-09-30"), 30);
});

test("Tagessummen addieren Volumen und Anzahl", () => {
  const days = [
    { day: "2026-09-01", currency: "CHF", volume: 0.1, deposits: 1, withdrawals: 0, transfers: 0 },
    { day: "2026-09-02", currency: "CHF", volume: 0.2, deposits: 0, withdrawals: 1, transfers: 2 }
  ];
  const totals = dailyTotals(days, "CHF");
  assert.equal(totals.volume, 0.3);
  assert.equal(totals.transfers, 2);
});

test("Achsenwerte haben immer Tausendertrennzeichen, grosse Werte werden kompakt", () => {
  assert.match(formatCompactMoney(7500), /^7['’]500$/);
  assert.match(formatCompactMoney(10000), /^10['’]000$/);
  assert.match(formatCompactMoney(2_500_000), /^2\.5\s?Mio\.?$/);
});

test("Der laufende Monat beginnt am Ersten und endet heute", () => {
  assert.deepEqual(currentMonthRange(new Date(2026, 8, 27)), { from: "2026-09-01", to: "2026-09-27" });
});
