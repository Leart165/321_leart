namespace Analytics.Infrastructure.Persistence;

// Das erzeugte PDF, getrennt vom Bericht: wer nur den Status abfragt, lädt nicht jedes Mal das
// ganze Dokument mit.
public sealed class ReportDocumentRecord
{
    public Guid ReportId { get; set; }

    public byte[] Content { get; set; } = Array.Empty<byte>();
}
