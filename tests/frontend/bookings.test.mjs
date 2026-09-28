import assert from "node:assert/strict";
import { test } from "node:test";
import {
  KIND_FILTERS, bookingTotals, filterBookings, formatBookedAt, kindLabel, lastDaysRange, monthRange, signedAmount
} from "../../src/Analytics.Api/wwwroot/js/domain/bookings.js";

const bookings = [
  { transactionId: "a", kind: "Withdrawal", amount: 0.1, currency: "CHF", bookedAt: "2026-09-26T12:15:00Z" },
  { transactionId: "b", kind: "Deposit", amount: 0.3, currency: "CHF", bookedAt: "2026-09-25T08:00:00Z" },
  { transactionId: "c", kind: "TransferIn", amount: 10, currency: "EUR", bookedAt: "2026-09-24T08:00:00Z" },
  { transactionId: "d", kind: "TransferOut", amount: 0.2, currency: "CHF", bookedAt: "2026-09-23T08:00:00Z" }
];

test("Einnahmen sind positiv, Ausgaben negativ", () => {
  assert.equal(signedAmount(bookings[0]), -0.1);
  assert.equal(signedAmount(bookings[1]), 0.3);
  assert.equal(signedAmount(bookings[2]), 10);
  assert.equal(signedAmount(bookings[3]), -0.2);
});

test("Summen rechnen in Rappen, ohne Gleitkommafehler", () => {
  const totals = bookingTotals(filterBookings(bookings, { currency: "CHF" }));
  assert.deepEqual(totals, { income: 0.3, expenses: 0.3, net: 0, count: 3 });
});

test("Der Filter trennt Einnahmen, Ausgaben und einzelne Arten und behält die Reihenfolge", () => {
  assert.deepEqual(filterBookings(bookings, { kind: "income" }).map((booking) => booking.transactionId), ["b", "c"]);
  assert.deepEqual(filterBookings(bookings, { kind: "expenses" }).map((booking) => booking.transactionId), ["a", "d"]);
  assert.deepEqual(filterBookings(bookings, { kind: "TransferOut" }).map((booking) => booking.transactionId), ["d"]);
  assert.equal(filterBookings(bookings, { kind: "all", currency: "EUR" }).length, 1);
});

test("Jede Art der Bank hat einen deutschen Namen und einen Filter", () => {
  for (const kind of ["Deposit", "Withdrawal", "TransferOut", "TransferIn"]) {
    assert.notEqual(kindLabel(kind), kind);
    assert.ok(KIND_FILTERS.some((filter) => filter.value === kind));
  }
});

test("Ohne Zeitraum gelten die letzten 30 Tage bis heute, wie in der API", () => {
  assert.deepEqual(lastDaysRange(30, new Date(2026, 8, 28)), { from: "2026-08-30", to: "2026-09-28" });
});

test("Ein Monat reicht vom ersten bis zum letzten Tag, auch im Schaltjahr", () => {
  assert.deepEqual(monthRange(2026, 9), { from: "2026-09-01", to: "2026-09-30" });
  assert.deepEqual(monthRange(2028, 2), { from: "2028-02-01", to: "2028-02-29" });
  assert.deepEqual(monthRange(2026, 12), { from: "2026-12-01", to: "2026-12-31" });
});

test("Der Zeitpunkt steht sekundengenau im Protokoll", () => {
  assert.equal(formatBookedAt("2026-09-26T12:15:07Z", "UTC"), "26.09.2026, 12:15:07");
  assert.equal(formatBookedAt("2026-09-26T12:15:07Z", "Europe/Zurich"), "26.09.2026, 14:15:07");
});
