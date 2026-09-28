# Demo: Learts Teil

Etwa sieben Minuten. Alles läuft im Gesamtsystem aus `m321-main`; Befehle aus dessen Verzeichnis.
Vorher offen haben: RabbitMQ (http://127.0.0.1:15672, Tab *Queues*), Grafana (http://127.0.0.1:3000,
Dashboard "Bank: Metriken", Bereich "Broker und Verarbeitung") und die Statistikseite
(http://analytics.localhost:8080/), angemeldet als Kunde und in einem zweiten Fenster als
`bank-admin`.

```bash
cd ../m321-main
ANALYTICS_TAG=local docker compose --profile observability up -d
```

## 1. Wer ich bin (30 s)

Analytics-Firma, externer Partner der Bank. Ich sehe keinen Code und keine Datenbank der Bank,
nur ihre Ereignisse über einen eigenen Broker-Benutzer und die Tokens ihrer Kunden. Diagramm aus
dem README zeigen.

## 2. Publish-Subscribe (1 min)

- Im Frontend der Bank eine Einzahlung machen.
- RabbitMQ, *Exchanges* → `bank.events` → *Bindings*: die Bank publiziert einmal, mehrere Queues
  hängen daran, darunter meine `analytics.partner`. Die Bank weiss nicht, wer zuhört.
- Statistikseite neu laden: die Summe ist da.
- Grafana, "Empfangene Nachrichten je Ergebnis": `analytics.partner`, `processed`.

## 3. Eigener Ausfall und Nachholen (1.5 min)

```bash
docker compose stop analytics-api          # beide Instanzen
```

- Im Frontend der Bank drei Buchungen machen. Die Bank arbeitet ungestört weiter.
- RabbitMQ: `analytics.partner` sammelt die Nachrichten (*Ready* steigt). Grafana, "Nachrichten
  in den Queues", zeigt den Rückstand.

```bash
docker compose start analytics-api
```

- Der Rückstand fällt auf null, die Statistikseite zeigt alle drei Buchungen. Nichts ging verloren,
  nichts zählt doppelt.

Variante Skalierung: `docker compose stop` nur einer Instanz (`docker stop m321-analytics-api-1`);
die zweite übernimmt, RabbitMQ zeigt einen Konsumenten weniger.

## 4. Scope, Rolle, Besitz (1.5 min)

- Statistikseite als Kunde: "Deine Freigabe" nennt `analytics:read`, "Nie: Konten, Salden oder
  Überweisungen". Der Reiter "Bank je Tag" fehlt.
- Dieselbe Anfrage mit dem Token des Kunden an `system/daily`: **403**, ohne `insufficient_scope`,
  denn mehr Zustimmung hilft nicht, ihm fehlt die Rolle. In der Konsole der Entwicklertools auf der
  Statistikseite:

  ```js
  const t = JSON.parse(sessionStorage["analytics.session"]).accessToken;
  const r = await fetch("v1/analytics/system/daily?from=2026-09-01&to=2026-09-30", { headers: { Authorization: `Bearer ${t}` } });
  console.log(r.status, r.headers.get("WWW-Authenticate"));   // 403 null
  ```
- Im Fenster mit `bank-admin`: "Bank je Tag" zeigt die Tagessummen, **200**.
- Swagger unter http://localhost:8080/analytics/swagger/index.html: an jedem Endpunkt das Schloss mit
  dem Scope `analytics:read`, das Schema `keycloak` mit den Adressen der Bank.
- In der App der Bank unter "Verbundene Dienste" den Zugriff der Analytics-Firma entziehen; bei der
  nächsten Anmeldung fragt die Bank wieder um Zustimmung.
- 403 **ohne Scope** lässt sich mit dem echten Keycloak nicht erzeugen, weil die Audience
  `m321-analytics-api` erst mit dem Scope ins Token kommt. Den Fall zeigt der Test:

```bash
dotnet test --filter "FullyQualifiedName~A_valid_token_without_the_scope"
```

## 5. Umstellung auf das Partner-Ereignis, Expand and Contract (2 min)

Das ist mein Breaking Change: die Bank gibt mir statt ihres internen Ereignisses nur noch ein
schlankes, ohne Buchungstext und Konto. Datenschutz.

**Expand** (Ausgangslage):

- RabbitMQ: zwei Queues, `analytics.ledger` (alt, an `transaction.completed`) und
  `analytics.partner` (neu, an `partner.transaction.completed`), beide mit zwei Konsumenten.
- Eine Buchung machen. Grafana: `analytics.partner` `processed` und `analytics.ledger`
  `duplicate`, oder umgekehrt. Sie kam zweimal und zählt einmal. In Loki:

  ```logql
  {service="analytics-api"} |= "Buchung"
  ```

  (Die JSON-Logs schreiben Umlaute als `\u00E4`; nach "gezählt" zu suchen fände deshalb nichts.)

**Contract**:

```bash
ANALYTICS_LEGACY_QUEUE=Retire ANALYTICS_TAG=local docker compose --profile observability up -d analytics-api
docker compose logs -f analytics-api | grep Contract
```

- Log: "Bindung von analytics.ledger an transaction.completed gelöst", dann "analytics.ledger ist
  leer und gelöscht". RabbitMQ zeigt nur noch `analytics.partner`.
- Eine Buchung machen: sie zählt weiter, jetzt nur noch über die Partner-Queue.

**Abschluss durch die Bank**:

```bash
ANALYTICS_EVENTS=partner-only docker compose up -d broker-setup
```

- Ab jetzt darf mein Benutzer nur noch `partner.*` abonnieren. Kein Ausfall, keine Buchung doppelt,
  keine verloren.

## Falls etwas klemmt

| Symptom | Grund, Abhilfe |
|---|---|
| `analytics.localhost` zeigt 404 von nginx | Traefik hat die Instanzen nach einem Neustart noch nicht übernommen; ein paar Sekunden warten |
| Anmeldung: "Client nicht gefunden" oder "Invalid redirect uri" | in `analytics-web` fehlt `http://analytics.localhost:8080/*`; Tim trägt es ein |
| Container `analytics-api` nicht gesund | läuft ein altes Image? `docker build ... -t ghcr.io/leart165/analytics-api:local` und neu starten |
| Broker lehnt ab (`ACCESS_REFUSED`) | `broker-setup` lief nicht; `docker compose up -d broker-setup` |
