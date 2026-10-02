using Analytics.Domain.Ledger;
using Analytics.Domain.Reports;
using Analytics.Infrastructure.Reports;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Analytics.Tests.Reports;

// Das PDF: gültiger Aufbau, jede Buchung als Zeile, Umlaute in WinAnsi, mehrere Seiten.
public sealed class PdfStatementRendererTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);
    private static readonly Currency Chf = Currency.Of("CHF");

    private readonly PdfStatementRenderer _renderer = new PdfStatementRenderer();

    [Fact]
    public void Every_booking_is_a_line_with_time_kind_signed_amount_and_transaction()
    {
        Guid transactionId = Guid.Parse("6f1c2c1e-6a3b-4f5e-9d7a-2b1c0d9e8f7a");
        MonthlyStatement statement = Statement(
            new StatementLine(transactionId, TransactionKind.Deposit, 1250.5m, Chf, MonthlyStatementTests.At(3, 14).AddSeconds(7)),
            MonthlyStatementTests.Line(TransactionKind.TransferOut, 20m, Chf, MonthlyStatementTests.At(4, 9)));

        string pdf = Text(_renderer.Render(statement));

        Assert.Contains("(Monatsbericht September 2026)", pdf);
        Assert.Contains("(kunde-1)", pdf);
        Assert.Contains("(03.09.2026 14:00:07)", pdf);
        Assert.Contains("(Einzahlung)", pdf);
        Assert.Contains("(+1'250.50)", pdf);
        Assert.Contains("(Überweisung gesendet)", pdf);
        Assert.Contains("(-20.00)", pdf);
        Assert.Contains($"({transactionId})", pdf);
        Assert.Contains("(+1'230.50)", pdf);
    }

    [Fact]
    public void The_document_is_a_valid_pdf_whose_cross_reference_table_points_at_every_object()
    {
        byte[] bytes = _renderer.Render(Statement(MonthlyStatementTests.Line(TransactionKind.Deposit, 1m, Chf, MonthlyStatementTests.At(1, 1))));
        string pdf = Text(bytes);

        Assert.StartsWith("%PDF-1.4\n", pdf);
        Assert.EndsWith("%%EOF\n", pdf);

        int startXref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)\n").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.StartsWith("xref\n", pdf[startXref..]);

        MatchCollection entries = Regex.Matches(pdf[startXref..], @"(\d{10}) 00000 n \n");
        Assert.NotEmpty(entries);
        for (int index = 0; index < entries.Count; index++)
        {
            int offset = int.Parse(entries[index].Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.StartsWith($"{index + 1} 0 obj\n", pdf[offset..]);
        }

        // Die Länge jedes Inhaltsstroms stimmt mit den Bytes überein.
        foreach (Match stream in Regex.Matches(pdf, @"<< /Length (\d+) >>\nstream\n"))
        {
            int length = int.Parse(stream.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.StartsWith("\nendstream", pdf[(stream.Index + stream.Length + length)..]);
        }
    }

    [Fact]
    public void Many_bookings_continue_on_further_pages_with_page_numbers()
    {
        StatementLine[] lines = Enumerable.Range(0, 120)
            .Select(index => MonthlyStatementTests.Line(TransactionKind.Deposit, 1m, Chf, MonthlyStatementTests.At(1 + (index % 28), 10)))
            .ToArray();

        string pdf = Text(_renderer.Render(Statement(lines)));

        int pages = Regex.Matches(pdf, "/Type /Page /Parent").Count;
        Assert.True(pages >= 3, $"Erwartet mindestens 3 Seiten, es sind {pages}.");
        Assert.Contains($"(Seite 1 von {pages})", pdf);
        Assert.Contains($"(Seite {pages} von {pages})", pdf);
        Assert.Equal(120, Regex.Matches(pdf, @"\(Einzahlung\)").Count);
    }

    [Fact]
    public void An_empty_month_says_so()
    {
        string pdf = Text(_renderer.Render(Statement()));

        Assert.Contains("(Keine Buchungen in diesem Monat.)", pdf);
        Assert.Contains("(Seite 1 von 1)", pdf);
    }

    [Fact]
    public void Characters_outside_winansi_are_replaced_and_brackets_are_escaped()
    {
        Assert.Equal("Grüsse \\(1\\) \u0080 ?", Analytics.Infrastructure.Reports.Pdf.WinAnsi.Escape("Grüsse (1) € 中"));
    }

    private static MonthlyStatement Statement(params StatementLine[] lines)
    {
        return MonthlyStatement.Create(MonthlyStatementTests.Report(), lines, Now);
    }

    // Jedes Byte ein Zeichen, wie das PDF es schreibt.
    private static string Text(byte[] pdf)
    {
        return Encoding.Latin1.GetString(pdf);
    }
}
