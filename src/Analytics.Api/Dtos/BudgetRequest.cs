using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Dtos;

public sealed record BudgetRequest(
    [Required][RegularExpression("^[A-Z]{3}$")] string? Currency,
    [Required][Range(typeof(decimal), "0.01", "1000000")] decimal? MonthlyLimit);
