using OpenTelemetry;
using System.Diagnostics;

namespace Analytics.Infrastructure.Observability;

// Datenbankspannen ohne Elternteil, etwa die Migration beim Start, wären sonst eigene Traces
// und würden in Tempo die Buchungen überdecken.
internal sealed class StandaloneDatabaseSpanFilter : BaseProcessor<Activity>
{
    public const string NpgsqlSource = "Npgsql";

    public override void OnStart(Activity activity)
    {
        if (activity.Source.Name != NpgsqlSource || activity.Parent is not null || activity.ParentSpanId != default)
        {
            return;
        }

        activity.IsAllDataRequested = false;
        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }
}
