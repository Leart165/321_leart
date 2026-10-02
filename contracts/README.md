# Kontrakte

Die Wahrheit liegt im Shared-Repo `Tim-Fischer-zh/m321-main` unter `contracts/`. Dieser Ordner
ist nur eine Kopie, damit Build, Tests und die eingebetteten Schemas ohne zweites Repository
funktionieren. Die CI vergleicht ihn bei jedem Lauf mit dem Shared-Repo und bricht ab, wenn sie
auseinanderlaufen.

Ändern immer zuerst in `m321-main`, dann hier aktualisieren:

```bash
cp -R ../m321-main/contracts/. contracts/
```

Was dieser Dienst davon benutzt:

| Datei | Wofür |
|---|---|
| `analytics/openapi.v1.yaml` | eigene HTTP-API, Version 1 |
| `analytics/openapi.v2.yaml` | eigene HTTP-API, Version 2 mit `/v2/analytics/me/overview` |
| `analytics/asyncapi.v1.yaml` | eigene Queue `analytics.ledger` an `transaction.completed` |
| `events/asyncapi.v1.yaml` | Schema von `TransactionCompleted`, zur Laufzeit gegen jede Nachricht geprüft |
| `accounts/openapi.v2.yaml` | daraus wird beim Build der Client für den Kontendienst erzeugt |
| `auth/jwt.md` | Aufbau der Keycloak-Token |

Veröffentlichte `*.v1.yaml` sind unveränderlich. Wer etwas anderes braucht, legt `*.v2.yaml`
daneben.
