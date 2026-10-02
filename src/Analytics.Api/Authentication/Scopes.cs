namespace Analytics.Api.Authentication;

// Was ein Client im Namen des Kunden bei uns darf, festgelegt in contracts/auth/scopes.md. Ein
// Zugriff braucht drei Dinge zugleich: den Scope (was der Client darf), die Rolle (was der
// Benutzer darf) und die Besitzregel (sub gleich ownerId, nur eigene Summen).
public static class Scopes
{
    // Der Claim, in dem Keycloak die gewährten Scopes durch Leerzeichen getrennt nennt.
    public const string Claim = "scope";

    public const string AnalyticsRead = "analytics:read";

    public static readonly string[] All = { AnalyticsRead };

    // Im Wortlaut von contracts/auth/scopes.md; erscheint in der Spezifikation.
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [AnalyticsRead] = "Eigene Summen und Buchungen bei der Analytics-Firma lesen"
    };
}
