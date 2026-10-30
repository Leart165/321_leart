using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Analytics.Infrastructure.Messaging;

// Gemeinsame Namen am eigenen Broker der Analytics-Firma. Mit dem Broker der Bank spricht dieser
// Dienst nicht mehr: die Buchungen kommen über HTTP, siehe Bank/StatementsBookings.
public static class MessagingTopology
{
    // Jede Queue hat über diesen Exchange ihre eigene Dead-Letter-Queue; der Routing Key ist der
    // Name der Queue.
    public const string DeadLetterExchange = "analytics.dlx";

    // Gibt die Anzahl wartender Nachrichten zurück, oder null, wenn es die Queue nicht gibt.
    // Eine passive Deklaration einer fehlenden Queue schliesst den Kanal, deshalb ein eigener.
    public static async Task<uint?> CountIfExistsAsync(IConnection connection, string queue, CancellationToken cancellationToken)
    {
        await using IChannel probe = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            QueueDeclareOk declared = await probe.QueueDeclarePassiveAsync(queue, cancellationToken);
            return declared.MessageCount;
        }
        catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == Constants.NotFound)
        {
            return null;
        }
    }
}
