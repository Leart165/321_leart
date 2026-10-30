namespace Analytics.Infrastructure.Bank;

public sealed class BankOptions
{
    public const string SectionName = "Bank";

    // Der normale Eingang der Bank, im Gesamtsystem http://traefik:8080. Nicht 8081: der nimmt nur
    // Anfragen aus dem Netz der Bank an.
    public Uri BaseUrl { get; set; } = new Uri("http://localhost:8080");
}
