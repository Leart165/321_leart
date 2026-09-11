namespace Auth.Infrastructure.AccountsApi;

public sealed class AccountsApiOptions
{
    public const string SectionName = "AccountsApi";

    public string BaseUrl { get; set; } = "http://accounts-api:8080";
}
