using Analytics.Infrastructure.Messaging;
using Xunit;

namespace Analytics.Tests.Messaging;

// Die eingebetteten Schemas aus contracts/events und contracts/partner entscheiden zur Laufzeit,
// ob eine Nachricht der Bank verarbeitet wird.
public sealed class AsyncApiSchemasTests
{
    private static readonly AsyncApiSchemas Schemas = AsyncApiSchemas.FromEmbeddedContracts();

    public const string ValidBooking = """
        {
          "transactionId": "5f0c2b1e-8d5e-4a55-9a53-2b7f0f1b1c11",
          "accountId": "7d1f5c3a-1c2b-4e5f-8a9b-0c1d2e3f4a5b",
          "ownerId": "kunde-1",
          "kind": "Deposit",
          "amount": 100.50,
          "currency": "CHF",
          "description": "Lohn",
          "bookedAt": "2026-09-17T09:30:00Z"
        }
        """;

    // Dieselbe Buchung, wie die Bank sie einem Partner gibt: ohne Konto und Buchungstext.
    public const string ValidPartnerBooking = """
        {
          "transactionId": "5f0c2b1e-8d5e-4a55-9a53-2b7f0f1b1c11",
          "ownerId": "kunde-1",
          "kind": "Deposit",
          "amount": 100.50,
          "currency": "CHF",
          "bookedAt": "2026-09-17T09:30:00Z"
        }
        """;

    [Fact]
    public void A_booking_as_the_bank_publishes_it_internally_is_valid()
    {
        Assert.Empty(Schemas.Validate("TransactionCompleted", ValidBooking));
    }

    [Fact]
    public void A_partner_booking_as_the_bank_publishes_it_is_valid()
    {
        Assert.Empty(Schemas.Validate("PartnerTransactionCompleted", ValidPartnerBooking));
    }

    [Theory]
    [InlineData("\"amount\": 100.50,", "")]
    [InlineData("\"Deposit\"", "\"Bonus\"")]
    [InlineData("\"CHF\"", "\"chf\"")]
    [InlineData("\"amount\": 100.50", "\"amount\": -1")]
    public void A_booking_that_breaks_the_contract_is_invalid(string original, string replacement)
    {
        string broken = ValidBooking.Replace(original, replacement, StringComparison.Ordinal);

        Assert.NotEmpty(Schemas.Validate("TransactionCompleted", broken));
    }

    // Das Partner-Schema ist geschlossen: ein Konto oder ein Buchungstext darf gar nicht darin stehen.
    [Theory]
    [InlineData("\"ownerId\": \"kunde-1\",", "\"ownerId\": \"kunde-1\", \"accountId\": \"7d1f5c3a-1c2b-4e5f-8a9b-0c1d2e3f4a5b\",")]
    [InlineData("\"ownerId\": \"kunde-1\",", "\"ownerId\": \"kunde-1\", \"description\": \"Lohn\",")]
    [InlineData("\"ownerId\": \"kunde-1\",", "")]
    [InlineData("\"Deposit\"", "\"Bonus\"")]
    public void A_partner_booking_that_breaks_the_contract_is_invalid(string original, string replacement)
    {
        string broken = ValidPartnerBooking.Replace(original, replacement, StringComparison.Ordinal);

        Assert.NotEmpty(Schemas.Validate("PartnerTransactionCompleted", broken));
    }

    [Fact]
    public void An_unknown_message_type_is_reported()
    {
        Assert.False(Schemas.Knows("SomethingElse"));
        Assert.NotEmpty(Schemas.Validate("SomethingElse", "{}"));
    }
}
