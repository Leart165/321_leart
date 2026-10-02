// Fachlogik der Statistikseite: reine Funktionen, ohne DOM und ohne Netz.
//
// Beträge kommen als JSON-Zahlen. Summiert wird in Rappen als Ganzzahl, damit sich keine
// Gleitkommafehler aufsummieren (0.1 + 0.2 ergäbe sonst 0.30000000000000004).

const toCents = (amount) => Math.round(Number(amount) * 100);
const fromCents = (cents) => cents / 100;

export const MONTHS = ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

export function currenciesOf(...lists) {
  const codes = new Set();
  for (const list of lists) {
    for (const entry of list ?? []) {
      codes.add(entry.currency);
    }
  }
  return [...codes].sort();
}

export function yearTotals(months, currency) {
  const own = months.filter((month) => month.currency === currency);
  const income = own.reduce((sum, month) => sum + toCents(month.income), 0);
  const expenses = own.reduce((sum, month) => sum + toCents(month.expenses), 0);
  return {
    currency,
    income: fromCents(income),
    expenses: fromCents(expenses),
    net: fromCents(income - expenses),
    transactions: own.reduce((sum, month) => sum + month.transactions, 0)
  };
}

// Immer zwölf Monate, auch die ohne Buchung: im Diagramm fehlt sonst die Lücke.
export function monthSeries(months, currency) {
  const series = MONTHS.map((label, index) => ({ month: index + 1, label, income: 0, expenses: 0, net: 0, transactions: 0 }));
  for (const month of months) {
    if (month.currency !== currency || month.month < 1 || month.month > 12) {
      continue;
    }
    const slot = series[month.month - 1];
    slot.income = Number(month.income);
    slot.expenses = Number(month.expenses);
    slot.net = fromCents(toCents(month.income) - toCents(month.expenses));
    slot.transactions = month.transactions;
  }
  return series;
}

export function dailyTotals(days, currency) {
  const own = days.filter((day) => day.currency === currency);
  return {
    currency,
    volume: fromCents(own.reduce((sum, day) => sum + toCents(day.volume), 0)),
    deposits: own.reduce((sum, day) => sum + day.deposits, 0),
    withdrawals: own.reduce((sum, day) => sum + day.withdrawals, 0),
    transfers: own.reduce((sum, day) => sum + day.transfers, 0),
    days: own.length
  };
}

// Jeder Tag im Zeitraum, auch ohne Buchung, damit die Zeitachse gleichmässig bleibt.
export function daySeries(days, currency, from, to) {
  const byDay = new Map(days.filter((day) => day.currency === currency).map((day) => [day.day, day]));
  const series = [];
  for (let date = parseDay(from); date <= parseDay(to); date.setUTCDate(date.getUTCDate() + 1)) {
    const key = formatIsoDay(date);
    const day = byDay.get(key);
    series.push({
      day: key,
      volume: day ? Number(day.volume) : 0,
      deposits: day?.deposits ?? 0,
      withdrawals: day?.withdrawals ?? 0,
      transfers: day?.transfers ?? 0
    });
  }
  return series;
}

export function formatMoney(amount, currency) {
  try {
    return new Intl.NumberFormat("de-CH", { style: "currency", currency, minimumFractionDigits: 2 }).format(amount);
  } catch {
    return `${Number(amount).toFixed(2)} ${currency}`;
  }
}

// Für Achsen: ganze Zahlen mit Tausendertrennzeichen, ab einer Million kompakt ("1,2 Mio.").
export function formatCompactMoney(amount) {
  const compact = Math.abs(amount) >= 1_000_000;
  return new Intl.NumberFormat("de-CH", {
    notation: compact ? "compact" : "standard",
    maximumFractionDigits: compact ? 1 : 0,
    useGrouping: "always"
  }).format(amount);
}

export function formatCount(count) {
  return new Intl.NumberFormat("de-CH").format(count);
}

export function formatDay(isoDay) {
  const date = parseDay(isoDay);
  return new Intl.DateTimeFormat("de-CH", { day: "2-digit", month: "2-digit", timeZone: "UTC" }).format(date);
}

export function parseDay(isoDay) {
  const [year, month, day] = isoDay.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

export function formatIsoDay(date) {
  return date.toISOString().slice(0, 10);
}

export function currentMonthRange(today = new Date()) {
  const from = new Date(Date.UTC(today.getFullYear(), today.getMonth(), 1));
  const to = new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()));
  return { from: formatIsoDay(from), to: formatIsoDay(to) };
}

export function daysBetween(from, to) {
  return Math.round((parseDay(to) - parseDay(from)) / 86_400_000) + 1;
}
