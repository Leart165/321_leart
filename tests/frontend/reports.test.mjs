import assert from "node:assert/strict";
import { test } from "node:test";
import {
  defaultReportMonth, isPending, mergeReport, monthName, parseMonth, reportFileName, reportMonths, statusLabel
} from "../../src/Analytics.Api/wwwroot/js/domain/reports.js";

test("Wählbar sind die letzten zwölf Monate bis zum laufenden, neueste zuerst", () => {
  const months = reportMonths(new Date(2026, 9, 2));
  assert.equal(months.length, 12);
  assert.equal(months[0].value, "2026-10");
  assert.equal(months[1].value, "2026-09");
  assert.equal(months[11].value, "2025-11");
  assert.deepEqual({ year: months[1].year, month: months[1].month }, { year: 2026, month: 9 });
});

test("Vorgewählt ist der letzte abgeschlossene Monat, auch über den Jahreswechsel", () => {
  assert.equal(defaultReportMonth(new Date(2026, 9, 2)), "2026-09");
  assert.equal(defaultReportMonth(new Date(2027, 0, 15)), "2026-12");
});

test("Ein Monat wird nur im Format JJJJ-MM gelesen", () => {
  assert.deepEqual(parseMonth("2026-09"), { year: 2026, month: 9 });
  assert.equal(parseMonth("2026-9"), null);
  assert.equal(parseMonth(undefined), null);
});

test("Nur ein beantragter Bericht wird weiter abgefragt", () => {
  assert.equal(isPending({ status: "requested" }), true);
  assert.equal(isPending({ status: "ready" }), false);
  assert.equal(isPending({ status: "failed" }), false);
  assert.equal(statusLabel("ready"), "Bereit");
});

test("Ein neuer Stand ersetzt den Bericht an seiner Stelle, ein neuer Bericht kommt nach oben", () => {
  const reports = [{ reportId: "a", status: "requested" }, { reportId: "b", status: "ready" }];
  assert.deepEqual(mergeReport(reports, { reportId: "a", status: "ready" }).map((report) => report.status), ["ready", "ready"]);
  assert.deepEqual(mergeReport(reports, { reportId: "c", status: "requested" }).map((report) => report.reportId), ["c", "a", "b"]);
});

test("Name und Datei des Berichts", () => {
  assert.equal(monthName(2026, 9), "September 2026");
  assert.equal(monthName(2026, 3), "März 2026");
  assert.equal(reportFileName({ year: 2026, month: 9 }), "buchungen-2026-09.pdf");
});
