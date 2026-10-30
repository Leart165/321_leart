using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Dtos;

public sealed record AnnualReportRequest([Required][Range(2000, 2100)] int? Year);
