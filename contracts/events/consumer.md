# Neue Konsumentenbindung: analytics.ledger

Ergänzung zu `contracts/events/asyncapi.v1.yaml` im Shared-Repo. Das Schema
`TransactionCompleted` ändert sich nicht, der Analytics-Dienst bindet nur eine eigene Queue an
denselben Kanal `transactionCompleted`, genau wie `statements.ledger` das schon tut.

Unter `operations:` ergänzen:

```yaml
  consumeTransactionCompletedForAnalytics:
    x-queue:
      name: analytics.ledger
      consumer: analytics-api
      durable: true
      prefetch: 10
      deadLetterExchange: bank.dlx
      deadLetterQueue: analytics.ledger.dlq
    action: receive
    channel:
      $ref: '#/channels/transactionCompleted'
    summary: analytics-api verdichtet die Buchung, Queue analytics.ledger.
    description: |
      Der Auswertungsdienst führt daraus seine eigenen Summen je Inhaber/Monat und je Tag/Bank.
      Er speichert nie die einzelne Buchung. Dedupliziert wird über transactionId in derselben
      Transaktion wie die Summenaktualisierung, nicht über message-id: transactionId ist die
      fachliche Kennung der Buchung und bleibt bei einer erneuten Zustellung derselben Buchung
      gleich, auch falls message-id sich unterscheiden sollte.
```

Bindet sich, wie `statements.ledger`, an den bestehenden Topic-Exchange `bank.events` mit dem
Routing-Key `transaction.completed` und erhält als eigene Dead-Letter-Queue
`analytics.ledger.dlq` am Direct-Exchange `bank.dlx`, mit sich selbst als Dead-Letter-Routing-Key
— exakt das in `contracts/events/asyncapi.v1.yaml` beschriebene Muster.

Referenzimplementierung: `src/Analytics.Infrastructure/Messaging/MessagingTopology.cs` und
`TransactionCompletedConsumer.cs` in diesem Repo.
