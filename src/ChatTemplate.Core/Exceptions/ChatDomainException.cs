namespace ChatTemplate.Core.Exceptions;

/// <summary>
/// Basisklasse fuer alle fachlichen (erwarteten) Chat-Domaenenfehler.
/// Diese Exceptions werden vom SignalRErrorHandlingFilter erkannt und 1:1
/// (mit Klartext-Meldung) als HubException an den aufrufenden Client zurueckgegeben,
/// im Gegensatz zu unerwarteten Systemfehlern, die generisch + TraceId geloggt werden.
/// </summary>
public abstract class ChatDomainException : Exception
{
    protected ChatDomainException(string message) : base(message)
    {
    }

    protected ChatDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
