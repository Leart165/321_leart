using Analytics.Api.Authentication;
using Analytics.Api.Dtos;
using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Analytics.Api.Controllers;

[ApiController]
[Route("v1/analytics")]
[Produces("application/json")]
[Authorize]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IMonthlyTotalsReader _monthlyTotals;

    public AnalyticsController(IMonthlyTotalsReader monthlyTotals)
    {
        _monthlyTotals = monthlyTotals;
    }

    [HttpGet("me/monthly")]
    [ProducesResponseType<IReadOnlyList<MonthlyTotalDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MonthlyTotalDto>>> GetMyMonthlyAsync(
        [FromQuery][Range(2000, 2100)] int year,
        CancellationToken cancellationToken)
    {
        string ownerId = User.FindFirstValue(JwtAuthentication.SubjectClaim)!;

        IReadOnlyList<MonthlyTotal> totals = await _monthlyTotals.GetForOwnerAsync(ownerId, year, cancellationToken);
        return Ok(totals.Select(MonthlyTotalDto.From).ToList());
    }
}
