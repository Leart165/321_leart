# Kontrakte

Der Analytics-Dienst gehört, wie alle Dienste der Bank-App, fachlich in `m321-main/contracts/`.
Dieses Repo enthält hier nur den Entwurf: `m321-main` existiert noch nicht (weder unter
`Leart165` noch unter `Tim-Fischer-zh` auffindbar), daher gibt es aktuell keinen Ort, an dem
die Wahrheit liegen könnte.

`analytics/openapi.v1.yaml` beschreibt die beiden HTTP-Endpunkte des Dienstes,
`events/consumer.md` beschreibt die zusätzliche Konsumentenbindung an `transaction.completed`,
die in `contracts/events/asyncapi.v1.yaml` des Shared-Repos ergänzt werden muss.

Sobald `m321-main` existiert:

```bash
cp contracts/analytics/openapi.v1.yaml   ../m321-main/contracts/analytics/openapi.v1.yaml
# und die Operation aus events/consumer.md manuell in
# ../m321-main/contracts/events/asyncapi.v1.yaml ergänzen
```

Danach richtet sich dieser Ordner, wie bei den anderen Diensten, nur noch nach der Kopie aus
dem Shared-Repo.
