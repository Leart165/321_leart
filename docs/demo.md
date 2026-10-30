# Demo: Learts Teil

Etwa zehn Minuten. Alles läuft im Gesamtsystem aus `m321-main`; Befehle aus dessen Verzeichnis.
Vorher offen haben: den eigenen RabbitMQ (http://127.0.0.1:15674, Tab *Queues*), das eigene
Grafana (http://127.0.0.1:3001, Ordner "Analytics": Logs, Metriken, Traces), das Grafana der Bank
(http://127.0.0.1:3000) und die Statistikseite (http://localhost:8091/, der zweite Eingang von
Traefik, solange Tim `analytics.localhost` nicht freigeschaltet hat), angemeldet als Kunde und in einem zweiten Fenster
als `bank-admin`.

```bash
cd ../m321-main
ANALYTICS_TAG=local docker compose -f docker-compose.yml -f ../override-port-8091.yml --profile observability up -d
```

## 1. Wer ich bin (30 s)

Analytics-Firma, externer Partner der Bank. Ich sehe keinen Code, keine Datenbank und keinen
Broker der Bank. Ich lese die Buchungen eines Kunden über HTTP, mit seinem Token und nur mit seiner
Zustimmung. Diagramm aus dem README zeigen.

## 2. Synchron über die Firmengrenze (1.5 min)

- Im Frontend der Bank eine Einzahlung machen.
- Statistikseite, Reiter "Buchungen", neu laden: die Einzahlung steht oben, sekundengenau.
- Dazu sagen: "Mein Dienst fragt in diesem Moment den Auszugsdienst der Bank,
  `GET /v2/statements/bookings`, mit dem Token des Kunden. Der Client ist aus dem Kontrakt der Bank
  erzeugt; ändert sie ihn, kompiliert mein Dienst nicht mehr."
- Statusleiste unten: die Korrelations-Id anklicken. **Mein** Grafana (3001), Explore, Loki:
  `{service="analytics-api"} |= "<Korrelations-Id>"` zeigt meine Zeilen. Dieselbe Id im Grafana
  **der Bank** (3000) zeigt die Zeile ihres Auszugsdienstes: "Client analytics-web liest …
  Buchungen von …". Jede Firma sieht nur ihre eigenen Logs, verbunden über die Id.
- Mein Grafana, Explore, Tempo, Dienst `analytics-api`: der Aufruf an die Bank als eigene Spanne.
  Die Trace-Id kopieren und im Tempo der Bank suchen: dort Traefik und statements-api, derselbe
  Trace, die andere Hälfte.

## 3. Zustimmung (1 min)

- Statistikseite, unten "Deine Freigabe": `analytics:read` und `statements:read`, "Nie: Konten,
  Salden oder Überweisungen".
- Dazu sagen: "`statements:read` schreibt Keycloak nur nach der Zustimmung ins Token. Fehlt sie oder
  lehnt die Bank ab, antworte ich 403 mit `scope="statements:read"` und lösche, was ich von diesem
  Kunden gespeichert habe."
- In der App der Bank unter "Verbundene Dienste" den Zugriff entziehen; bei der nächsten Anmeldung
  fragt die Bank wieder.
- Den Fall ohne Zustimmung zeigen die Tests:

```bash
dotnet test --filter "FullyQualifiedName~Without_consent|FullyQualifiedName~A_refusal_of_the_bank"
```

## 4. Die Bank fällt aus (1.5 min)

```bash
docker compose -f docker-compose.yml -f ../override-port-8091.yml stop statements-api
```

- Statistikseite neu laden: die Buchungen sind noch da, die Statusleiste sagt "Bank nicht
  erreichbar, gespeicherter Stand". Die Antwort trägt `X-Data-Source: stored`.
- Mein Grafana, Dashboard "Analytics: Metriken", Bereiche "Aufrufe bei der Bank" und "Resilienz":
  Wiederholungen und `OnCircuitOpened`. In den Logs darunter "Kreis zur Bank offen für 00:00:15": nach fünf
  Fehlversuchen fragt der Dienst die Bank 15 s lang gar nicht mehr.
- Einen Monatsbericht anfordern: 503, kein halber Antrag bleibt liegen.

```bash
docker compose -f docker-compose.yml -f ../override-port-8091.yml start statements-api
```

- Nach der Pause neu laden: die Statusleiste meldet nichts mehr, die Zahlen kommen wieder frisch.

## 5. Monatsbericht über den eigenen Broker (1.5 min)

- Reiter "Buchungen", unten "Berichte als PDF": Monat wählen, **Monatsbericht anfordern**.
- Die Zeile steht sofort da mit "Wird erstellt …": die API hat die Buchungen des Monats bei der
  Bank geholt und mit dem Antrag und einem Eintrag in der Outbox in *einer* Transaktion
  gespeichert, dann **202** geantwortet. Ein Token geht nie in die Queue.
- Nach etwa einer Sekunde "Bereit", dann **Herunterladen**.
- Eigener RabbitMQ (15674), *Exchanges* → `analytics.events`; *Queues* → `analytics.reports` mit
  2 Consumers und `analytics.reports.dlq`. Dazu sagen: "Mein eigener Broker, nur im Netz partner.
  Die Bank kommt nicht daran, ich nicht an ihren." Im eigenen Dashboard "Konsumenten je Queue".

## 6. Jahresauszug und Budgetwarnung (2 min)

- Reiter "Buchungen", unten "Berichte als PDF": Jahr wählen, **Jahresauszug anfordern**. Die Zeile
  heisst "Jahresauszug 2025", nach etwa einer Sekunde **Herunterladen**: `jahresauszug-2025.pdf`.
- Eigener RabbitMQ: `analytics.annual-reports` mit eigener DLQ. Dazu sagen: "Eine eigene Queue,
  damit ein grosser Jahresauszug keinen Monatsbericht aufhält."
- Reiter "Meine Übersicht", Karte "Monatsbudget": 100 eingeben, **Festlegen**. Der Balken zeigt,
  wie viel diesen Monat schon ausgegeben ist.
- Im Frontend der Bank 90 auszahlen, dann die Statistikseite neu laden. Der Dienst speichert die
  neue Buchung, schickt `budget.check.requested` in `analytics.budgets`, der Konsument legt die
  Warnung an. Oben erscheint "Budget fast aufgebraucht"; nach weiteren 20 "Budget überschritten".
- Dazu sagen: "Die Anfrage wartet nicht auf die Prüfung. Jede Stufe kommt einmal im Monat, auch
  wenn beide Instanzen gleichzeitig prüfen: dafür sorgt ein eindeutiger Index."
- Mein Grafana, Explore, Loki: `{service="analytics-api"} |= "Budget"` zeigt "Warnung angelegt".

## 7. Skalierung und eigener Ausfall (1 min)

```bash
docker stop m321-analytics-api-1
```

- Statistikseite neu laden: Traefik schickt alles an die zweite Instanz. Eigener RabbitMQ:
  `analytics.reports`, `analytics.annual-reports` und `analytics.budgets` haben je einen Consumer
  weniger, Berichte und Warnungen entstehen weiter.

```bash
docker start m321-analytics-api-1
```

## 8. Expand and Contract über die Firmengrenze (1 min)

- **Expand**, Tim: `GET /v2/statements/bookings` in `statements/openapi.v2.yaml` 2.1.0, additiv.
- **Migrate**, ich: Buchungen über HTTP, eigener Broker, der Konsument am Broker der Bank ist weg.
  `contracts/analytics/asyncapi.v2.yaml` ist als abgelöst markiert und bleibt als Nachweis.
- **Contract**, Tim: `partner.transaction.completed` nicht mehr veröffentlichen, Benutzer
  `analytics` weg, Broker der Bank raus aus dem Netz `partner`.
- Keine Firma musste gleichzeitig mit der anderen deployen.

## Falls etwas klemmt

| Symptom | Grund, Abhilfe |
|---|---|
| `analytics.localhost` zeigt 404 von nginx | Traefik hat die Instanzen nach einem Neustart noch nicht übernommen; ein paar Sekunden warten |
| Anmeldung: "Client nicht gefunden" oder "Invalid redirect uri" | in `analytics-web` fehlt `http://analytics.localhost:8080/*`; Tim trägt es ein |
| Seite: "Du hast uns nicht erlaubt, deine Buchungen bei der Bank zu lesen" | das Token hat kein `statements:read`; abmelden, neu anmelden und bei der Bank zustimmen |
| Seite: "Bank vorübergehend nicht erreichbar" | Auszugsdienst weg und nichts gespeichert; `docker compose ps statements-api` |
| Container `analytics-api` nicht gesund | läuft ein altes Image? `docker build ... -t ghcr.io/leart165/analytics-api:local` und neu starten |
