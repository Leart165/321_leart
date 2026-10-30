namespace Analytics.Application.Ledger;

public sealed record LedgerSnapshot(IReadOnlyList<BookingEntry> Bookings, BookingSource Source);

public sealed record MonthlyOverview(IReadOnlyList<MonthlyTotal> Months, BookingSource Source);
