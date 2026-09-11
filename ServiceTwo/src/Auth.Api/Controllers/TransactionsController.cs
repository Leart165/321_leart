using Auth.Api.Dtos;
using Auth.Application.Transactions;
using Auth.Domain.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route("v1/transactions")]
[Produces("application/json")]
public sealed class TransactionsController : ControllerBase
{
    private readonly TransactionLogService _transactionLog;

    public TransactionsController(TransactionLogService transactionLog)
    {
        _transactionLog = transactionLog;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetLogAsync([FromQuery] string ownerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Transaction> transactions = await _transactionLog.GetTransactionLogAsync(ownerId, cancellationToken);

        IReadOnlyList<TransactionDto> body = transactions.Select(TransactionDto.From).ToList();

        return Ok(body);
    }
}
