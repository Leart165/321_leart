namespace Analytics.Infrastructure.Messaging;

// Welches Ereignis der Bank in einer Queue liegt.
public enum BookingFormat
{
    // transaction.completed nach contracts/events/asyncapi.v1.yaml, nur noch in analytics.ledger.
    Internal,

    // partner.transaction.completed nach contracts/partner/asyncapi.v1.yaml, in analytics.partner.
    Partner
}
