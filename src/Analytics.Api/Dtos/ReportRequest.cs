using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Dtos;

public sealed record ReportRequest(
    [Required][Range(2000, 2100)] int? Year,
    [Required][Range(1, 12)] int? Month);
