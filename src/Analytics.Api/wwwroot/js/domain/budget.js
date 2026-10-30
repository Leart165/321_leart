// Monatsbudget: reine Funktionen, ohne DOM und ohne Netz. Die Stufen rechnet der Dienst; hier
// werden sie nur beschrieben.

import { formatMoney } from "./figures.js";
import { monthName } from "./reports.js";

export const LEVEL_TEXT = {
  ok: "Im Rahmen",
  warning: "Bald aufgebraucht",
  exceeded: "Überschritten"
};

export function levelLabel(level) {
  return LEVEL_TEXT[level] ?? level;
}

// Ob oben auf der Seite gewarnt wird.
export function needsWarning(budget) {
  return budget?.level === "warning" || budget?.level === "exceeded";
}

// Für den Balken: höchstens 100, auch wenn mehr ausgegeben wurde.
export function barWidth(budget) {
  return Math.max(0, Math.min(100, budget?.percent ?? 0));
}

export function spentText(budget) {
  return `${formatMoney(budget.spent, budget.currency)} von ${formatMoney(budget.monthlyLimit, budget.currency)} (${budget.percent} %)`;
}

export function warningText(budget) {
  const month = monthName(budget.year, budget.month);
  if (budget.level === "exceeded") {
    return `Budget für ${month} überschritten: ${spentText(budget)}.`;
  }
  return `Budget für ${month} fast aufgebraucht: ${spentText(budget)}.`;
}

// Eingabe wie "2000", "2'000" oder "1500.50"; null, wenn es kein gültiger Betrag ist.
export function parseLimit(text) {
  const cleaned = String(text ?? "").replace(/['’\s]/g, "").replace(",", ".");
  if (!/^\d+(\.\d{1,2})?$/.test(cleaned)) {
    return null;
  }
  const value = Number(cleaned);
  return value > 0 && value <= 1_000_000 ? value : null;
}
