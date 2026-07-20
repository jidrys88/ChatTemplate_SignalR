namespace ChatTemplate.Core.Exceptions;

/// <summary>Wird geworfen, wenn ein angefragter Chat-Raum nicht existiert.</summary>
public sealed class RoomNotFoundException(string roomId)
    : ChatDomainException($"Der Chat-Raum '{roomId}' wurde nicht gefunden.")
{
    public string RoomId { get; } = roomId;
}

/// <summary>
/// Wird geworfen, wenn ein User eine Aktion durchfuehren moechte, fuer die er
/// nicht berechtigt ist (z.B. Nachricht an einen Raum senden, dem er nicht beigetreten ist).
/// </summary>
public sealed class UnauthorizedChatActionException(string reason)
    : ChatDomainException($"Aktion nicht erlaubt: {reason}");

/// <summary>Wird geworfen, wenn eine Nachricht fachliche Validierungsregeln verletzt (z.B. leer/zu lang).</summary>
public sealed class InvalidMessageException(string reason)
    : ChatDomainException($"Ungueltige Nachricht: {reason}");

/// <summary>Wird geworfen, wenn ein Zielbenutzer fuer eine Direktnachricht nicht gefunden/online ermittelt werden kann.</summary>
public sealed class RecipientNotFoundException(string userId)
    : ChatDomainException($"Empfaenger '{userId}' wurde nicht gefunden.")
{
    public string UserId { get; } = userId;
}
