using Analytics.Api.Authentication;
using Analytics.Api.Dtos;
using Analytics.Api.Middleware;
using Analytics.Application.Reports;
using Analytics.Domain.Exceptions;
using Analytics.Domain.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Controllers.V2;

// Monatsberichte als PDF beantragen, verfolgen und abholen. Wie die Überweisung der Bank: der
// Antrag wird angenommen und sofort mit 202 beantwortet, erzeugt wird über den Exchange
// analytics.events und die Queue analytics.reports. Siehe contracts/analytics/openapi.v2.yaml.
//
// Besitzregel wie beim Buchungsprotokoll: der Inhaber kommt aus dem Token. Ein fremder Bericht
// wird beantwortet wie ein unbekannter.
[ApiController]
[Route("v2/analytics/me/reports")]
[Produces("application/json")]
[Authorize(Policy = Scopes.AnalyticsRead)]
public sealed class ReportsController : ControllerBase
{
    private const string GetRouteName = "V2GetReport";

    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReportDto>> RequestAsync([FromBody] ReportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            MonthlyReport report = await _reports.RequestAsync(
                User.OwnerId(), request.Year!.Value, request.Month!.Value, CorrelationId.Of(HttpContext), cancellationToken);
            return AcceptedAtRoute(GetRouteName, new { reportId = report.Id }, ReportDto.From(report));
        }
        catch (InvalidReportRequestException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, "Eingabe ungültig", exception.Message);
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<MonthlyReport> reports = await _reports.ListOwnedAsync(User.OwnerId(), cancellationToken);
        return Ok(reports.Select(ReportDto.From).ToList());
    }

    [HttpGet("{reportId:guid}", Name = GetRouteName)]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDto>> GetAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            MonthlyReport report = await _reports.GetOwnedAsync(reportId, User.OwnerId(), cancellationToken);
            return Ok(ReportDto.From(report));
        }
        catch (ReportNotFoundException exception)
        {
            return Problem(StatusCodes.Status404NotFound, "Bericht unbekannt", exception.Message);
        }
    }

    [HttpGet("{reportId:guid}/document")]
    [Produces("application/pdf", "application/problem+json")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDocumentAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            ReportDocument document = await _reports.GetDocumentAsync(reportId, User.OwnerId(), cancellationToken);
            return File(document.Content, document.ContentType, document.FileName);
        }
        catch (ReportNotFoundException exception)
        {
            return Problem(StatusCodes.Status404NotFound, "Bericht unbekannt", exception.Message);
        }
        catch (ReportNotReadyException exception)
        {
            return Problem(StatusCodes.Status409Conflict, "Bericht nicht bereit", exception.Message);
        }
    }

    private ObjectResult Problem(int status, string title, string detail)
    {
        ProblemDetails problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
