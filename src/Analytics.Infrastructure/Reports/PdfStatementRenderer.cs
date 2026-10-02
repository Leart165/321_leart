using Analytics.Application.Abstractions;
using Analytics.Domain.Ledger;
using Analytics.Domain.Reports;
using Analytics.Infrastructure.Reports.Pdf;
using System.Globalization;

namespace Analytics.Infrastructure.Reports;

// Druckt einen Monatsbericht als PDF, schwarz auf weiss: Kopf, Summen je Währung, dann jede
// Buchung als Zeile wie auf einem Kontoauszug. Reicht eine Seite nicht, folgt die nächste mit
// demselben Tabellenkopf.
public sealed class PdfStatementRenderer : IStatementRenderer
{
    private const double Left = 50;
    private const double Right = PdfDocumentWriter.PageWidth - 50;
    private const double Top = PdfDocumentWriter.PageHeight - 50;
    private const double Bottom = 60;
    private const double RowHeight = 15;

    // Spalten der Buchungstabelle.
    private const double TimeColumn = Left;
    private const double KindColumn = 150;
    private const double AmountRightEdge = 330;
    private const double CurrencyColumn = 340;
    private const double TransactionColumn = 375;

    // Spalten der Summentabelle, rechte Kanten.
    private const double IncomeRightEdge = 210;
    private const double ExpensesRightEdge = 310;
    private const double NetRightEdge = 410;
    private const double CountRightEdge = Right;

    private const double Body = 9;
    private const double Small = 7.5;

    // Ohne Kulturdaten: das alpine-Image läuft ohne ICU, de-CH gäbe es dort nicht.
    private static readonly string[] MonthNames =
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    private static readonly NumberFormatInfo SwissNumbers = new NumberFormatInfo
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = "'",
        NegativeSign = "-"
    };

    public string ContentType
    {
        get { return "application/pdf"; }
    }

    public byte[] Render(MonthlyStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        List<PdfCanvas> pages = new List<PdfCanvas>();
        PdfCanvas page = new PdfCanvas();
        pages.Add(page);

        double y = WriteHeader(page, statement);
        y = WriteTotals(page, statement, y);

        page.Text(Left, y, PdfFont.Bold, 11, "Buchungen");
        y -= RowHeight + 2;

        if (statement.Lines.Count == 0)
        {
            page.Text(Left, y, PdfFont.Regular, Body, "Keine Buchungen in diesem Monat.");
        }
        else
        {
            y = WriteBookingHeader(page, y);
            foreach (StatementLine line in statement.Lines)
            {
                if (y < Bottom)
                {
                    page = new PdfCanvas();
                    pages.Add(page);
                    y = WriteBookingHeader(page, Top);
                }

                WriteBooking(page, line, y);
                y -= RowHeight;
            }
        }

        string title = $"Monatsbericht {MonthName(statement.Period)}";
        for (int index = 0; index < pages.Count; index++)
        {
            WriteFooter(pages[index], statement, index + 1, pages.Count);
        }

        return PdfDocumentWriter.Write(pages, title, statement.GeneratedAt);
    }

    private static double WriteHeader(PdfCanvas page, MonthlyStatement statement)
    {
        double y = Top;
        page.Text(Left, y, PdfFont.Bold, 18, $"Monatsbericht {MonthName(statement.Period)}");
        y -= 16;
        page.Text(Left, y, PdfFont.Regular, Body, "Buchungsprotokoll der Analytics-Firma, im Auftrag der Bank");
        y -= 22;

        page.Text(Left, y, PdfFont.Bold, Body, "Kunde");
        page.Text(KindColumn, y, PdfFont.Regular, Body, statement.OwnerId);
        y -= RowHeight - 2;
        page.Text(Left, y, PdfFont.Bold, Body, "Zeitraum");
        page.Text(KindColumn, y, PdfFont.Regular, Body,
            $"{Day(statement.Period.FirstDay)} bis {Day(statement.Period.LastDay)}, Zeiten in UTC");
        y -= RowHeight - 2;
        page.Text(Left, y, PdfFont.Bold, Body, "Erstellt");
        page.Text(KindColumn, y, PdfFont.Regular, Body, Timestamp(statement.GeneratedAt));
        y -= 10;

        page.Line(Left, y, Right, y, 0.8, 0);
        return y - 24;
    }

    private static double WriteTotals(PdfCanvas page, MonthlyStatement statement, double y)
    {
        page.Text(Left, y, PdfFont.Bold, 11, "Summen");
        y -= RowHeight + 2;

        if (statement.Totals.Count == 0)
        {
            page.Text(Left, y, PdfFont.Regular, Body, "Keine Buchungen, keine Summen.");
            return y - 30;
        }

        page.Text(Left, y, PdfFont.Bold, Body, "Währung");
        page.TextRight(IncomeRightEdge, y, PdfFont.MonoBold, Body, "Einnahmen");
        page.TextRight(ExpensesRightEdge, y, PdfFont.MonoBold, Body, "Ausgaben");
        page.TextRight(NetRightEdge, y, PdfFont.MonoBold, Body, "Netto");
        page.TextRight(CountRightEdge, y, PdfFont.MonoBold, Body, "Buchungen");
        y -= 5;
        page.Line(Left, y, Right, y, 0.5, 0);
        y -= RowHeight - 3;

        foreach (StatementTotal total in statement.Totals)
        {
            page.Text(Left, y, PdfFont.Regular, Body, total.Currency.Code);
            page.TextRight(IncomeRightEdge, y, PdfFont.Mono, Body, Money(total.Income));
            page.TextRight(ExpensesRightEdge, y, PdfFont.Mono, Body, Money(total.Expenses));
            page.TextRight(NetRightEdge, y, PdfFont.Mono, Body, Signed(total.Net));
            page.TextRight(CountRightEdge, y, PdfFont.Mono, Body, total.Bookings.ToString(CultureInfo.InvariantCulture));
            y -= RowHeight;
        }

        return y - 18;
    }

    private static double WriteBookingHeader(PdfCanvas page, double y)
    {
        page.Text(TimeColumn, y, PdfFont.Bold, Body, "Zeitpunkt (UTC)");
        page.Text(KindColumn, y, PdfFont.Bold, Body, "Art");
        page.TextRight(AmountRightEdge, y, PdfFont.MonoBold, Body, "Betrag");
        page.Text(CurrencyColumn, y, PdfFont.Bold, Body, "Whg.");
        page.Text(TransactionColumn, y, PdfFont.Bold, Body, "Transaktion");
        y -= 5;
        page.Line(Left, y, Right, y, 0.5, 0);
        return y - (RowHeight - 3);
    }

    private static void WriteBooking(PdfCanvas page, StatementLine line, double y)
    {
        page.Text(TimeColumn, y, PdfFont.Regular, Body, Timestamp(line.BookedAt));
        page.Text(KindColumn, y, PdfFont.Regular, Body, KindLabel(line.Kind));
        page.TextRight(AmountRightEdge, y, PdfFont.Mono, Body, Signed(line.SignedAmount));
        page.Text(CurrencyColumn, y, PdfFont.Regular, Body, line.Currency.Code);
        page.Text(TransactionColumn, y, PdfFont.Mono, 7, line.TransactionId.ToString());
        page.Line(Left, y - 4.5, Right, y - 4.5, 0.25, 0.8);
    }

    private static void WriteFooter(PdfCanvas page, MonthlyStatement statement, int number, int count)
    {
        page.Line(Left, 42, Right, 42, 0.5, 0);
        page.Text(Left, 30, PdfFont.Regular, Small, $"Analytics-Firma, Monatsbericht {statement.Period}, nur Buchungen aus dem Partner-Ereignis der Bank");
        page.Text(Right - 50, 30, PdfFont.Regular, Small, $"Seite {number} von {count}");
    }

    public static string KindLabel(TransactionKind kind)
    {
        return kind switch
        {
            TransactionKind.Deposit => "Einzahlung",
            TransactionKind.Withdrawal => "Auszahlung",
            TransactionKind.TransferIn => "Überweisung erhalten",
            TransactionKind.TransferOut => "Überweisung gesendet",
            _ => kind.ToString()
        };
    }

    public static string MonthName(ReportPeriod period)
    {
        return $"{MonthNames[period.Month - 1]} {period.Year}";
    }

    public static string Signed(decimal amount)
    {
        return amount < 0m ? "-" + Money(-amount) : "+" + Money(amount);
    }

    public static string Money(decimal amount)
    {
        return amount.ToString("#,##0.00", SwissNumbers);
    }

    private static string Day(DateOnly day)
    {
        return day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static string Timestamp(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
