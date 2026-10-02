# JWT-Kontrakt

Was in einem Token stehen muss, damit der Kontendienst es akzeptiert. Ausgestellt vom
Keycloak auf `auth.fischerfocus.com`, Realm `personal`. Geprüft wird es vom Kontendienst.
Der Auszugsdienst reicht es nur weiter, siehe unten.

## Aussteller

| Was | Wert |
|-----|------|
| Issuer | `https://auth.fischerfocus.com/realms/personal` |
| Discovery | `https://auth.fischerfocus.com/realms/personal/.well-known/openid-configuration` |
| Schlüssel (JWKS) | `https://auth.fischerfocus.com/realms/personal/protocol/openid-connect/certs` |
| Client | `m321-accounts-api` |

Keycloak bietet **kein** `/.well-known/jwks.json` an. Wo die Schlüssel liegen, steht im
Discovery-Dokument; der Kontendienst liest es von dort und braucht nur den Issuer.

## Signatur

| Was | Wert |
|-----|------|
| Algorithmus | ES256, jeder andere wird abgelehnt |
| Schlüsselwechsel | über `kid` im Kopf, der Kontendienst lädt den Schlüsselsatz nach |

Kein HS256, weil dann Aussteller und Prüfer dasselbe Geheimnis kennen müssten. Das wäre eine
Kopplung, die genau das aufweicht, was das Projekt zeigen soll. Der Algorithmus ist fest
vorgegeben und wird nicht aus dem Token übernommen, damit ein Angreifer ihn nicht wählen kann.

## Pflicht-Claims

| Claim | Bedeutung |
|-------|-----------|
| `iss` | Aussteller, muss genau dem Issuer oben entsprechen |
| `sub` | Benutzer-Id. **Das ist die `ownerId` beim Kontendienst.** Fehlt er, gilt das Token als ungültig |
| `aud` | muss `m321-accounts-api` enthalten |
| `typ` | muss `Bearer` sein, also ein Access Token. Ein ID Token (`ID`) wird abgelehnt, auch wenn Audience und Benutzer stimmen |
| `exp` | Ablauf |
| `iat` | Ausstellungszeitpunkt |

`aud` sagt, **für wen** das Token bestimmt ist, `azp` nur, **welcher Client** es geholt hat.
Geprüft wird `aud`. Keycloak setzt `aud` nicht von selbst auf den Client, dafür braucht der
Client einen Mapper vom Typ "Audience" mit `m321-accounts-api` als "Included Client Audience".

## Optionale Claims

| Claim | Bedeutung |
|-------|-----------|
| `email` | Nur zur Anzeige, nie zur Autorisierung |
| `name` | Nur zur Anzeige |

## Was der Kontendienst damit tut

Ab Version 2 der Accounts-API liest er die `ownerId` aus `sub`, statt sie aus dem Request zu
nehmen. Ein Aufrufer sieht und bebucht dann nur noch seine eigenen Konten, ein fremdes Konto
beantwortet er wie ein unbekanntes mit 404. Version 1 bleibt daneben bestehen und ignoriert
das Token, bis alle Konsumenten migriert sind.

Der Auszugsdienst prüft selbst kein Token, sondern reicht das Token des Kunden unverändert an
den Kontendienst weiter. Er hat kein eigenes Dienstkonto.

## Token holen

Benutzer werden in Keycloak angelegt, es gibt keine Registrierung über die Bank-App. Ein
Token für Entwicklung und Demo holt das Skript `tools/token/token.sh` im Repo des
Kontendienstes, und zwar ein echtes vom Keycloak, kein selbst signiertes:

```bash
TOKEN=$(tools/token/token.sh tim)
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/v2/accounts
```
