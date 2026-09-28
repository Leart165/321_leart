using Npgsql;
using System.Net.Sockets;

namespace Analytics.Infrastructure.Persistence;

public static class DatabaseFailures
{
    // Die Datenbank ist gerade nicht da oder antwortet nicht rechtzeitig. EF Core packt die
    // eigentliche Ausnahme von Npgsql gern in eine oder zwei Hüllen, deshalb die ganze Kette.
    public static bool IsTransient(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true } or SocketException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
