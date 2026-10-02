using System.Globalization;
using System.Text;

namespace Analytics.Infrastructure.Reports.Pdf;

// Schreibt ein PDF 1.4 aus fertigen Seiten: Katalog, Seitenbaum, Schriften, je Seite ein
// Seitenobjekt und ein unkomprimierter Inhaltsstrom, am Ende die Querverweistabelle mit den
// Byte-Positionen aller Objekte. Mehr braucht ein Bericht aus Text und Linien nicht.
public static class PdfDocumentWriter
{
    // A4 in Punkt.
    public const double PageWidth = 595;
    public const double PageHeight = 842;

    private const int CatalogId = 1;
    private const int PagesId = 2;
    private const int InfoId = 3;
    private const int FirstFontId = 4;

    public static byte[] Write(IReadOnlyList<PdfCanvas> pages, string title, DateTimeOffset createdAt)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("Ein PDF braucht mindestens eine Seite.", nameof(pages));
        }

        int firstPageId = FirstFontId + PdfFont.All.Count;
        List<int> pageIds = Enumerable.Range(0, pages.Count).Select(index => firstPageId + (index * 2)).ToList();
        int objectCount = firstPageId + (pages.Count * 2) - 1;

        using MemoryStream output = new MemoryStream();
        long[] offsets = new long[objectCount + 1];

        // Die zweite Zeile mit Bytes über 127 sagt Programmen, dass die Datei binär ist.
        Write(output, "%PDF-1.4\n%âãÏÓ\n");

        WriteObject(output, offsets, CatalogId, $"<< /Type /Catalog /Pages {PagesId} 0 R >>");

        string kids = string.Join(' ', pageIds.Select(id => $"{id} 0 R"));
        WriteObject(output, offsets, PagesId, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");

        WriteObject(output, offsets, InfoId,
            $"<< /Title ({WinAnsi.Escape(title)}) /Producer (analytics-api) /CreationDate ({PdfDate(createdAt)}) >>");

        string fontResources = string.Join(' ', PdfFont.All.Select((font, index) => $"/{font.ResourceName} {FirstFontId + index} 0 R"));
        for (int index = 0; index < PdfFont.All.Count; index++)
        {
            WriteObject(output, offsets, FirstFontId + index,
                $"<< /Type /Font /Subtype /Type1 /BaseFont /{PdfFont.All[index].BaseFont} /Encoding /WinAnsiEncoding >>");
        }

        for (int index = 0; index < pages.Count; index++)
        {
            int pageId = pageIds[index];
            int contentId = pageId + 1;

            WriteObject(output, offsets, pageId,
                $"<< /Type /Page /Parent {PagesId} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] " +
                $"/Resources << /Font << {fontResources} >> >> /Contents {contentId} 0 R >>");

            byte[] content = pages[index].ToBytes();
            offsets[contentId] = output.Position;
            Write(output, $"{contentId} 0 obj\n<< /Length {content.Length} >>\nstream\n");
            output.Write(content);
            Write(output, "\nendstream\nendobj\n");
        }

        long xref = output.Position;
        StringBuilder table = new StringBuilder();
        table.Append("xref\n0 ").Append(objectCount + 1).Append('\n');
        table.Append("0000000000 65535 f \n");
        for (int id = 1; id <= objectCount; id++)
        {
            table.Append(offsets[id].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        table.Append("trailer\n<< /Size ").Append(objectCount + 1)
            .Append(" /Root ").Append(CatalogId).Append(" 0 R /Info ").Append(InfoId).Append(" 0 R >>\n")
            .Append("startxref\n").Append(xref).Append("\n%%EOF\n");
        Write(output, table.ToString());

        return output.ToArray();
    }

    private static void WriteObject(MemoryStream output, long[] offsets, int id, string body)
    {
        offsets[id] = output.Position;
        Write(output, $"{id} 0 obj\n{body}\nendobj\n");
    }

    private static void Write(MemoryStream output, string text)
    {
        output.Write(Encoding.Latin1.GetBytes(text));
    }

    private static string PdfDate(DateTimeOffset value)
    {
        return "D:" + value.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";
    }
}
