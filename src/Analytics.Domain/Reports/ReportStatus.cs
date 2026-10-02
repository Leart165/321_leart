namespace Analytics.Domain.Reports;

// Zustand eines Monatsberichts. Erlaubt sind genau zwei Übergänge, beide von Requested aus;
// ein abgeschlossener Bericht ändert sich nie wieder. Wie der Überweisungsauftrag der Bank.
//
//   Requested ──► Ready
//   Requested ──► Failed
//
// Im Kontrakt stehen dieselben Namen klein geschrieben.
public enum ReportStatus
{
    Requested,
    Ready,
    Failed
}
