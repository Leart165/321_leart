using Analytics.Api.Authentication;
using Analytics.Api.Dtos;
using Analytics.Api.Middleware;
using Analytics.Application.Budgets;
using Analytics.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers.V2;

// Das Budget für die Ausgaben eines Monats. Erreichen die Ausgaben 80 oder 100 Prozent, legt der
// Konsument der Queue analytics.budgets eine Warnung an; GET zeigt sie mit dem Stand des Monats.
// Siehe contracts/analytics/openapi.v2.yaml.
//
// Gerechnet wird mit den Buchungen von der Bank. Ohne Zustimmung (statements:read) gibt es auch
// kein Budget: 403 wie bei den Buchungen.
[ApiController]
[Route("v2/analytics/me/budget")]
[Produces("application/json")]
[Authorize(Policy = Scopes.AnalyticsRead)]
public sealed class BudgetController : ControllerBase
{
    private readonly BudgetService _budgets;

    public BudgetController(BudgetService budgets)
    {
        _budgets = budgets;
    }

    [HttpGet]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetDto>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            BudgetStatus status = await _budgets.GetStatusAsync(User.OwnerId(), User.HasConsentedToBankBookings(), cancellationToken);
            return Ok(BudgetDto.From(status));
        }
        catch (BudgetNotFoundException exception)
        {
            return Problem(StatusCodes.Status404NotFound, "Kein Budget", exception.Message);
        }
    }

    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BudgetDto>> PutAsync([FromBody] BudgetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            BudgetStatus status = await _budgets.SetAsync(
                User.OwnerId(),
                User.HasConsentedToBankBookings(),
                request.Currency!,
                request.MonthlyLimit!.Value,
                CorrelationId.Of(HttpContext),
                cancellationToken);
            return Ok(BudgetDto.From(status));
        }
        catch (Exception exception) when (exception is InvalidBudgetException or InvalidLedgerEventException)
        {
            return Problem(StatusCodes.Status400BadRequest, "Eingabe ungültig", exception.Message);
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _budgets.RemoveAsync(User.OwnerId(), User.HasConsentedToBankBookings(), cancellationToken);
            return NoContent();
        }
        catch (BudgetNotFoundException exception)
        {
            return Problem(StatusCodes.Status404NotFound, "Kein Budget", exception.Message);
        }
    }

    private ObjectResult Problem(int status, string title, string detail)
    {
        ProblemDetails problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
