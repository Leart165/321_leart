namespace Analytics.Application.Ledger;

public enum BookingSource
{
    // Frisch von der Bank.
    Live,

    // Die Bank war nicht zu erreichen; das ist der zuletzt gespeicherte Stand.
    Stored
}
