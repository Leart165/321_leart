namespace Analytics.Application.Reports;

public enum ReportGeneration
{
    // PDF erzeugt, Bericht ist Ready.
    Generated,

    // Gescheitert, aus einem Grund, den eine Wiederholung nicht behebt. Bericht ist Failed.
    Failed,

    // Schon erledigt, etwa bei einer zweiten Zustellung derselben Nachricht. Nichts geändert.
    AlreadyCompleted,

    // Den Bericht gibt es nicht.
    Unknown
}
