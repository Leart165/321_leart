namespace Analytics.Application.Ledger;

// NewBookings: wie viele Buchungen dabei zum ersten Mal gespeichert wurden.
public sealed record LedgerSnapshot(IReadOnlyList<BookingEntry> Bookings, BookingSource Source, int NewBookings);

public sealed record MonthlyOverview(IReadOnlyList<MonthlyTotal> Months, BookingSource Source, int NewBookings);
