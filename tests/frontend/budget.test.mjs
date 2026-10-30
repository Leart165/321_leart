import assert from "node:assert/strict";
import { test } from "node:test";
import { barWidth, levelLabel, needsWarning, parseLimit, warningText } from "../../src/Analytics.Api/wwwroot/js/domain/budget.js";

const budget = (level, percent, spent) => ({
  currency: "CHF", monthlyLimit: 1000, year: 2026, month: 10, spent, percent, level, alerts: []
});

test("Gewarnt wird ab der Stufe warning, nicht im Rahmen und nicht ohne Budget", () => {
  assert.equal(needsWarning(budget("ok", 79, 790)), false);
  assert.equal(needsWarning(budget("warning", 80, 800)), true);
  assert.equal(needsWarning(budget("exceeded", 120, 1200)), true);
  assert.equal(needsWarning(null), false);
});

test("Der Balken endet bei 100 Prozent, auch wenn mehr ausgegeben wurde", () => {
  assert.equal(barWidth(budget("ok", 45, 450)), 45);
  assert.equal(barWidth(budget("exceeded", 130, 1300)), 100);
  assert.equal(barWidth(null), 0);
});

test("Die Warnung nennt Monat, Betrag und Prozent", () => {
  const text = warningText(budget("exceeded", 120, 1200));
  assert.match(text, /^Budget für Oktober 2026 überschritten/);
  assert.match(text, /120 %/);
  assert.match(warningText(budget("warning", 85, 850)), /fast aufgebraucht/);
  assert.equal(levelLabel("warning"), "Bald aufgebraucht");
});

test("Ein Budget ist ein Betrag über 0 mit höchstens zwei Nachkommastellen", () => {
  assert.equal(parseLimit("2000"), 2000);
  assert.equal(parseLimit("2'000"), 2000);
  assert.equal(parseLimit("1500,50"), 1500.5);
  assert.equal(parseLimit("0"), null);
  assert.equal(parseLimit("12.345"), null);
  assert.equal(parseLimit("-5"), null);
  assert.equal(parseLimit("abc"), null);
  assert.equal(parseLimit("2000000"), null);
});
