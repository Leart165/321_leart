using Analytics.Api.Authentication;
using Analytics.Api.Bank;
using Analytics.Api.Dtos;
using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Controllers.V2;

// Version 2: dieselben Summen wie Version 1 und dazu das Buchungsprotokoll, siehe
// contracts/analytics/openapi.v2.yaml. Buchungen und Summen kommen von der Bank, gelesen mit dem
// Token des Kunden; ist sie nicht erreichbar, der gespeicherte Stand, markiert in X-Data-Source.
[ApiController]
[Route("v2/analytics")]
[Produces("application/json")]
[Authorize(Policy = Scopes.AnalyticsRead)]
public sealed class AnalyticsController : ControllerBase
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    private readonly CustomerLedger _ledger;
    private readonly IDailyTotalsReader _dailyTotals;

    public AnalyticsController(CustomerLedger ledger, IDailyTotalsReader dailyTotals)
    {
        _ledger = ledger;
        _dailyTotals = dailyTotals;
    }

    // Jede einzelne Buchung des Inhabers, neueste zuerst. Ohne Angabe die letzten 30 Tage.
    [HttpGet("me/bookings")]
    [ProducesResponseType<IReadOnlyList<BookingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMyBookingsAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery][Range(1, 1000)] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        DateOnly last = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly first = from ?? last.AddDays(-(DefaultDays - 1));

        if (first > last)
        {
            return Invalid("'from' darf nicht nach 'to' liegen.");
        }

        if (last.DayNumber - first.DayNumber + 1 > MaxDays)
        {
            return Invalid($"Der Zeitraum darf höchstens {MaxDays} Tage umfassen.");
        }

        // Besitzregel: der Inhaber kommt aus dem Token, nie aus der Anfrage.
        LedgerSnapshot snapshot = await _ledger.GetBookingsAsync(
            User.OwnerId(), User.HasConsentedToBankBookings(), first, last, cancellationToken);
        DataSource.Report(Response, snapshot.Source);

        return Ok(snapshot.Bookings
            .OrderByDescending(entry => entry.BookedAt)
            .ThenBy(entry => entry.TransactionId)
            .Take(limit)
            .Select(BookingDto.From)
            .ToList());
    }

    [HttpGet("me/monthly")]
    [ProducesResponseType<IReadOnlyList<MonthlyTotalDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<MonthlyTotalDto>>> GetMyMonthlyAsync(
        [FromQuery][Range(2000, 2100)] int year,
        CancellationToken cancellationToken)
    {
        MonthlyOverview overview = await _ledger.GetMonthlyAsync(
            User.OwnerId(), User.HasConsentedToBankBookings(), year, cancellationToken);
        DataSource.Report(Response, overview.Source);
        return Ok(overview.Months.Select(MonthlyTotalDto.From).ToList());
    }

    // Nur über Kunden, deren Buchungen mit ihrer Zustimmung bei uns gespeichert sind: bankweite
    // Zahlen gibt die Bank keinem fremden Client. Die Rolle bank-admin prüft dieser Dienst selbst.
    [HttpGet("system/daily")]
    [Authorize(Roles = JwtAuthentication.AdminRole)]
    [ProducesResponseType<IReadOnlyList<DailyTotalDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DailyTotalDto>>> GetSystemDailyAsync(
        [FromQuery][BindRequired] DateOnly from,
        [FromQuery][BindRequired] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return Invalid("'from' darf nicht nach 'to' liegen.");
        }

        IReadOnlyList<DailyTotal> totals = await _dailyTotals.GetForRangeAsync(from, to, cancellationToken);
        return Ok(totals.Select(DailyTotalDto.From).ToList());
    }

    private BadRequestObjectResult Invalid(string detail)
    {
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Eingabe ungültig",
            Detail = detail
        });
    }
}
