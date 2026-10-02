using Analytics.Api.Authentication;
using Analytics.Api.Dtos;
using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Controllers.V2;

// Expand: Version 1 bleibt unverändert. Version 2 bietet dieselben Summen und dazu das
// Buchungsprotokoll, siehe contracts/analytics/openapi.v2.yaml.
[ApiController]
[Route("v2/analytics")]
[Produces("application/json")]
[Authorize(Policy = Scopes.AnalyticsRead)]
public sealed class AnalyticsController : ControllerBase
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    private readonly IMonthlyTotalsReader _monthlyTotals;
    private readonly IDailyTotalsReader _dailyTotals;
    private readonly IBookingLogReader _bookings;

    public AnalyticsController(IMonthlyTotalsReader monthlyTotals, IDailyTotalsReader dailyTotals, IBookingLogReader bookings)
    {
        _monthlyTotals = monthlyTotals;
        _dailyTotals = dailyTotals;
        _bookings = bookings;
    }

    // Jede einzelne Buchung des Inhabers, neueste zuerst. Ohne Angabe die letzten 30 Tage.
    [HttpGet("me/bookings")]
    [ProducesResponseType<IReadOnlyList<BookingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
        IReadOnlyList<BookingEntry> entries = await _bookings.GetForOwnerAsync(User.OwnerId(), first, last, limit, cancellationToken);
        return Ok(entries.Select(BookingDto.From).ToList());
    }

    [HttpGet("me/monthly")]
    [ProducesResponseType<IReadOnlyList<MonthlyTotalDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MonthlyTotalDto>>> GetMyMonthlyAsync(
        [FromQuery][Range(2000, 2100)] int year,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MonthlyTotal> totals = await _monthlyTotals.GetForOwnerAsync(User.OwnerId(), year, cancellationToken);
        return Ok(totals.Select(MonthlyTotalDto.From).ToList());
    }

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
