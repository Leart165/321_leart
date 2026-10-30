using Analytics.Application.Ledger;

namespace Analytics.Api.Bank;

// Sagt dem Aufrufer, ob die Zahlen frisch von der Bank kommen oder der gespeicherte Stand sind,
// weil die Bank gerade nicht zu erreichen war.
public static class DataSource
{
    public const string HeaderName = "X-Data-Source";

    public const string Live = "live";

    public const string Stored = "stored";

    public static void Report(HttpResponse response, BookingSource source)
    {
        response.Headers[HeaderName] = source == BookingSource.Live ? Live : Stored;
    }
}
