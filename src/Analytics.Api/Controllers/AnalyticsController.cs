using Analytics.Api.Authentication;
using Analytics.Api.Bank;
using Analytics.Api.Dtos;
using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("v1/analytics")]
[Produces("application/json")]
[Authorize(Policy = Scopes.AnalyticsRead)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly CustomerLedger _ledger;
    private readonly IDailyTotalsReader _dailyTotals;
    private readonly BudgetTrigger _budget;

    public AnalyticsController(CustomerLedger ledger, IDailyTotalsReader dailyTotals, BudgetTrigger budget)
    {
        _ledger = ledger;
        _dailyTotals = dailyTotals;
        _budget = budget;
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
        // Besitzregel: der Inhaber kommt aus dem Token, nie aus der Anfrage. Die Summen rechnet der
        // Dienst aus den Buchungen, die er mit dem Token des Kunden bei der Bank liest.
        MonthlyOverview overview = await _ledger.GetMonthlyAsync(
            User.OwnerId(), User.HasConsentedToBankBookings(), year, cancellationToken);
        DataSource.Report(Response, overview.Source);
        await _budget.AfterBookingsAsync(User.OwnerId(), overview.NewBookings, cancellationToken);
        return Ok(overview.Months.Select(MonthlyTotalDto.From).ToList());
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
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Eingabe ungültig",
                Detail = "'from' darf nicht nach 'to' liegen."
            });
        }

        IReadOnlyList<DailyTotal> totals = await _dailyTotals.GetForRangeAsync(from, to, cancellationToken);
        return Ok(totals.Select(DailyTotalDto.From).ToList());
    }
}
