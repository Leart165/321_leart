# Kontrakte

Die veröffentlichten Kontrakte liegen im Shared-Repo `Tim-Fischer-zh/m321-main` unter
`contracts/`. Dieser Ordner ist eine Kopie, damit Build, Tests und die eingebetteten Schemas ohne
zweites Repository funktionieren. Die CI holt das Shared-Repo nicht: eine CI, die ein fremdes Repo
holt, koppelt die Repos. Abgeglichen wird bei jedem Pull Request an `m321-main`.

Ändern immer zuerst per Pull Request in `m321-main`, dann hier aktualisieren:

```bash
cp -R ../m321-main/contracts/. contracts/
```

Nur `analytics/events.asyncapi.v1.yaml` liegt allein hier: er beschreibt die eigene interne Queue
am eigenen Broker, und die Bank hat damit nichts zu tun.

Was dieser Dienst davon benutzt:

| Datei | Wofür |
|---|---|
| `statements/openapi.v2.yaml` | ab 2.1.0 `GET /v2/statements/bookings`; daraus erzeugt NSwag beim Build den Client zur Bank |
| `analytics/openapi.v1.yaml` | eigene HTTP-API, Version 1, nur die Summen |
| `analytics/openapi.v2.yaml` | eigene HTTP-API, Version 2: Buchungsprotokoll, Monatssummen, Monatsbericht; vom Vertragstest mit der laufenden API verglichen |
| `analytics/events.asyncapi.v1.yaml` | eigener Exchange `analytics.events` und Queue `analytics.reports`; das Schema von `ReportRequested` wird zur Laufzeit gegen jede Nachricht geprüft |
| `analytics/asyncapi.v1.yaml`, `asyncapi.v2.yaml` | abgelöst, bleiben als Nachweis: die früheren Queues `analytics.ledger` und `analytics.partner` am Broker der Bank |
| `auth/jwt.md` | Aufbau und Prüfung der Keycloak-Token |
| `auth/scopes.md` | welcher Scope wofür nötig ist: `analytics:read` für diese API, `statements:read` für das Lesen bei der Bank; vom Vertragstest gelesen |

Veröffentlichte `*.v1.yaml` sind im Verhalten unveränderlich. Wer etwas anderes braucht, legt
`*.v2.yaml` daneben.
