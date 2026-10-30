namespace Analytics.Application.Bank;

// Der Kunde hat der Analytics-Firma nicht erlaubt, seine Buchungen bei der Bank zu lesen, oder
// die Erlaubnis zurückgenommen. Ein neuer Versuch hilft nicht, nur eine neue Zustimmung.
public sealed class BankAccessDeniedException : Exception
{
    public BankAccessDeniedException(string message)
        : base(message)
    {
    }

    public static BankAccessDeniedException WithoutConsent()
    {
        return new BankAccessDeniedException("Im Token fehlt die Zustimmung, die Buchungen bei der Bank zu lesen.");
    }
}
