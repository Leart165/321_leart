namespace Analytics.Infrastructure.Reports.Pdf;

// Die vier Standardschriften, die jeder PDF-Betrachter kennt. Sie werden nicht eingebettet; das
// hält das PDF klein, und das Image braucht keine Schriftdateien.
public sealed class PdfFont
{
    public static readonly PdfFont Regular = new PdfFont("F1", "Helvetica", isMonospaced: false);
    public static readonly PdfFont Bold = new PdfFont("F2", "Helvetica-Bold", isMonospaced: false);
    public static readonly PdfFont Mono = new PdfFont("F3", "Courier", isMonospaced: true);
    public static readonly PdfFont MonoBold = new PdfFont("F4", "Courier-Bold", isMonospaced: true);

    public static readonly IReadOnlyList<PdfFont> All = new[] { Regular, Bold, Mono, MonoBold };

    private PdfFont(string resourceName, string baseFont, bool isMonospaced)
    {
        ResourceName = resourceName;
        BaseFont = baseFont;
        IsMonospaced = isMonospaced;
    }

    public string ResourceName { get; }

    public string BaseFont { get; }

    public bool IsMonospaced { get; }
}
