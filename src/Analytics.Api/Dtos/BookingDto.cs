using Analytics.Application.Ledger;

namespace Analytics.Api.Dtos;

public sealed record BookingDto(
    Guid TransactionId,
    string Kind,
    decimal Amount,
    string Currency,
    DateTimeOffset BookedAt)
{
    public static BookingDto From(BookingEntry entry)
    {
        return new BookingDto(entry.TransactionId, entry.Kind, entry.Amount, entry.Currency, entry.BookedAt);
    }
}
