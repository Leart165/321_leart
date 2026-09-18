using System.ComponentModel.DataAnnotations;

namespace Analytics.Api.Authentication;

public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    [Required]
    [Url]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;
}
