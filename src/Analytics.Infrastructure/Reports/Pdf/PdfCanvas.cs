using System.Globalization;
using System.Text;

namespace Analytics.Infrastructure.Reports.Pdf;

// Inhalt einer PDF-Seite: Text und Linien, nur Schwarz und Grau. Koordinaten in Punkt, der
// Ursprung liegt unten links, wie in PDF üblich.
public sealed class PdfCanvas
{
    // Courier hat feste Breite: jedes Zeichen ist 600/1000 der Schriftgrösse breit. Nur damit
    // lässt sich ohne Breitentabellen rechtsbündig setzen, deshalb stehen Beträge in Courier.
    private const double CourierAdvance = 0.6;

    private readonly StringBuilder _content = new StringBuilder();

    public void Text(double x, double y, PdfFont font, double size, string text)
    {
        _content.Append("BT /").Append(font.ResourceName).Append(' ').Append(Number(size)).Append(" Tf ")
            .Append(Number(x)).Append(' ').Append(Number(y)).Append(" Td (")
            .Append(WinAnsi.Escape(text)).Append(") Tj ET\n");
    }

    public void TextRight(double right, double y, PdfFont font, double size, string text)
    {
        if (!font.IsMonospaced)
        {
            throw new ArgumentException("Rechtsbündig nur mit Courier, die anderen Schriften haben keine feste Breite.", nameof(font));
        }

        Text(right - (text.Length * size * CourierAdvance), y, font, size, text);
    }

    public void Line(double x1, double y1, double x2, double y2, double width, double gray)
    {
        _content.Append(Number(gray)).Append(" G ").Append(Number(width)).Append(" w ")
            .Append(Number(x1)).Append(' ').Append(Number(y1)).Append(" m ")
            .Append(Number(x2)).Append(' ').Append(Number(y2)).Append(" l S\n");
    }

    // Jedes Zeichen ist ein Byte in WinAnsi; Latin1 schreibt es unverändert.
    public byte[] ToBytes()
    {
        return Encoding.Latin1.GetBytes(_content.ToString());
    }

    private static string Number(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
