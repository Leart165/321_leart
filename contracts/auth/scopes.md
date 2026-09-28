# Scopes und Clients

Ergänzt `jwt.md`. Dort steht, wie ein Token aussehen muss, damit eine API es annimmt. Hier steht,
was ein Token erlaubt.

## Drei Fragen, drei Stellen

| Frage | Beispiel | Wo entschieden |
|---|---|---|
| Was darf dieser **Client** im Namen des Kunden? | Die Analytics-Firma darf Summen lesen, aber nicht überweisen | Scope im Token, gesetzt von Keycloak, geprüft von der API |
| Was darf dieser **Benutzer**? | Nur `bank-admin` sieht Daten aller Kunden: gescheiterte Aufträge bei der Bank, Tagessummen bei der Analytics-Firma | Realm-Rolle im Claim `realm_access` des Tokens, geprüft von der API |
| **Welche** Ressourcen? | Nur eigene Konten und Auszüge | Besitzregel im Dienst: `sub` gleich Inhaber, sonst 404 |

Ein Zugriff gelingt nur, wenn alle drei zustimmen. Ein Scope ersetzt weder die Rolle noch die
Besitzprüfung.

## Scopes

Keycloak schreibt die gewährten Scopes durch Leerzeichen getrennt in den Claim `scope`. Jeder
Scope bringt die Audience seiner API mit: ein Token enthält nur die Dienste, für die es Scopes hat.

| Scope | Bedeutung | Audience | Endpunkte |
|---|---|---|---|
| `accounts:read` | Eigene Konten, Salden und Aufträge lesen | `m321-accounts-api` | `GET /v2/accounts`, `GET /v2/accounts/{id}`, `GET /v2/transfers/{id}`; `GET /v2/admin/transfers/failed` zusätzlich nur mit Rolle `bank-admin` |
| `accounts:write` | Konto eröffnen, einzahlen, abheben | `m321-accounts-api` | `POST /v2/accounts`, `POST /v2/accounts/{id}/deposits`, `POST /v2/accounts/{id}/withdrawals` |
| `transfers:write` | Überweisungen von eigenen Konten beauftragen | `m321-accounts-api` | `POST /v2/transfers` |
| `statements:read` | Kontoauszüge eigener Konten lesen | `m321-statements-api` | `GET /v2/statements/{id}` |
| `analytics:read` | Eigene Summen bei der Analytics-Firma lesen | `m321-analytics-api` | `GET /v1/analytics/me/monthly`; `GET /v1/analytics/system/daily` zusätzlich nur mit Rolle `bank-admin` |

Version 1 der Bank-APIs verlangt kein Token und kennt deshalb keine Scopes. Sie ist abgelöst.

Die OpenAPI-Kontrakte der Version 2 nennen dieselben Scopes maschinenlesbar: das
Sicherheitsschema `keycloak` vom Typ OAuth2 mit den Flows Authorization Code und Client
Credentials listet die Scopes des Dienstes, und jede Operation nennt in `security` den Scope, den
sie verlangt. Die Vertragstests beider Dienste prüfen, dass der Code an jeder Operation genau
diesen Scope verlangt.

## Antworten

| Fall | Antwort |
|---|---|
| Kein oder ungültiges Token | 401, wie in `jwt.md` |
| Token gültig, Scope fehlt | 403 mit `WWW-Authenticate: Bearer error="insufficient_scope", scope="<fehlender Scope>"` nach RFC 6750 |
| Token und Scope gültig, Rolle fehlt | 403 ohne `insufficient_scope`: mehr Zustimmung des Kunden hilft nicht, dem Benutzer fehlt die Rolle |
| Token und Scope gültig, Ressource gehört jemand anderem | 404, wie eine unbekannte Ressource |

## Clients in Keycloak

| Client | Wem er gehört | Scopes | Zustimmung des Kunden |
|---|---|---|---|
| `m321-web` | Bank, Frontend unter `/` | `accounts:read`, `accounts:write`, `transfers:write`, `statements:read` | nein, es ist die Bank selbst |
| `m321-accounts-api` | Bank, Werkzeuge wie `tools/token/token.sh` | alle | nein |
| `m321-loadtest` | Bank, Lastwerkzeug `tools/stress/stress.py` | `accounts:read`, `accounts:write`, `transfers:write`, `statements:read` | nein; vertraulicher Client mit Client Credentials, die Konten gehören seinem Service Account |
| `analytics-web` | Analytics-Firma, "Sign in with Bank" | `analytics:read`; auf Wunsch `statements:read` | ja, Keycloak fragt den Kunden bei der ersten Anmeldung |

Alle Clients im Browser sind öffentlich, ohne Geheimnis, mit Authorization Code und PKCE (S256). Der
einzige vertrauliche Client ist `m321-loadtest`; sein Secret liegt nur in Keycloak und in der
Umgebung dessen, der den Lasttest startet.

Was ein Kunde einer fremden Firma erlaubt hat, sieht er im Frontend der Bank unter "Verbundene
Dienste" und kann es dort wieder entziehen. Die Zustimmungen verwaltet Keycloak; die Bank liest
sie über dessen Account-API.
