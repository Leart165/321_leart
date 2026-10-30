using Analytics.Infrastructure.Messaging;
using Xunit;

namespace Analytics.Tests.Messaging;

// Das eingebettete Schema aus contracts/analytics/events.asyncapi.v1.yaml entscheidet zur Laufzeit,
// ob ein Antrag auf einen Monatsbericht verarbeitet wird.
public sealed class AsyncApiSchemasTests
{
    private static readonly AsyncApiSchemas Schemas = AsyncApiSchemas.FromEmbeddedContracts();

    private const string ValidRequest = """
        {
          "reportId": "5f0c2b1e-8d5e-4a55-9a53-2b7f0f1b1c11",
          "year": 2026,
          "month": 9,
          "requestedAt": "2026-10-01T09:30:00Z"
        }
        """;

    [Fact]
    public void A_request_as_the_outbox_writes_it_is_valid()
    {
        Assert.Empty(Schemas.Validate(ReportTopology.ReportRequestedType, ValidRequest));
    }

    [Theory]
    [InlineData("\"5f0c2b1e-8d5e-4a55-9a53-2b7f0f1b1c11\"", "\"kein-guid\"")]
    [InlineData("\"month\": 9,", "")]
    [InlineData("\"month\": 9", "\"month\": 13")]
    public void A_request_that_breaks_the_contract_is_invalid(string original, string replacement)
    {
        string broken = ValidRequest.Replace(original, replacement, StringComparison.Ordinal);

        Assert.NotEmpty(Schemas.Validate(ReportTopology.ReportRequestedType, broken));
    }

    [Fact]
    public void An_unknown_message_type_is_reported()
    {
        Assert.False(Schemas.Knows("SomethingElse"));
        Assert.NotEmpty(Schemas.Validate("SomethingElse", "{}"));
    }
}
