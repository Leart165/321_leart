// Buchungsprotokoll: reine Funktionen, ohne DOM und ohne Netz.
//
// Die API liefert jeden Betrag positiv; ob er den Saldo erhöht oder vermindert, sagt die Art.
// Summiert wird wie in figures.js in Rappen als Ganzzahl.

import { formatIsoDay } from "./figures.js";

const toCents = (amount) => Math.round(Number(amount) * 100);
const fromCents = (cents) => cents / 100;

export const KINDS = {
  Deposit: { label: "Einzahlung", incoming: true },
  TransferIn: { label: "Überweisung erhalten", incoming: true },
  Withdrawal: { label: "Auszahlung", incoming: false },
  TransferOut: { label: "Überweisung gesendet", incoming: false }
};

export const KIND_FILTERS = [
  { value: "all", label: "Alle Buchungen" },
  { value: "income", label: "Nur Einnahmen" },
  { value: "expenses", label: "Nur Ausgaben" },
  ...Object.entries(KINDS).map(([kind, meta]) => ({ value: kind, label: meta.label }))
];

export function kindLabel(kind) {
  return KINDS[kind]?.label ?? kind;
}

export function isIncoming(kind) {
  return KINDS[kind]?.incoming === true;
}

// Positiv für Einnahmen, negativ für Ausgaben.
export function signedAmount(booking) {
  const cents = toCents(booking.amount);
  return fromCents(isIncoming(booking.kind) ? cents : -cents);
}

export function filterBookings(bookings, { kind = "all", currency = null } = {}) {
  return bookings.filter((booking) => {
    if (currency && booking.currency !== currency) {
      return false;
    }
    switch (kind) {
      case "all":
        return true;
      case "income":
        return isIncoming(booking.kind);
      case "expenses":
        return !isIncoming(booking.kind);
      default:
        return booking.kind === kind;
    }
  });
}

export function bookingTotals(bookings) {
  let income = 0;
  let expenses = 0;
  for (const booking of bookings) {
    if (isIncoming(booking.kind)) {
      income += toCents(booking.amount);
    } else {
      expenses += toCents(booking.amount);
    }
  }
  return { income: fromCents(income), expenses: fromCents(expenses), net: fromCents(income - expenses), count: bookings.length };
}

// Die letzten n Tage bis heute, heute eingeschlossen; wie der Standard der API.
export function lastDaysRange(days, today = new Date()) {
  const to = new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()));
  const from = new Date(to);
  from.setUTCDate(from.getUTCDate() - (days - 1));
  return { from: formatIsoDay(from), to: formatIsoDay(to) };
}

export function monthRange(year, month) {
  const from = new Date(Date.UTC(year, month - 1, 1));
  const to = new Date(Date.UTC(year, month, 0));
  return { from: formatIsoDay(from), to: formatIsoDay(to) };
}

// Zeitpunkt einer Buchung in der Zeitzone des Browsers, sekundengenau wie in einem Log.
export function formatBookedAt(isoDateTime, timeZone = undefined) {
  return new Intl.DateTimeFormat("de-CH", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    timeZone
  }).format(new Date(isoDateTime));
}
