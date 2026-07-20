namespace ChatTemplate.Core.Entities;

/// <summary>
/// Domaenen-Entitaet fuer eine einzelne Chat-Nachricht.
/// Wird sowohl fuer Gruppen- (Raum) als auch Direktnachrichten verwendet.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>Eindeutige ID der Nachricht (Primary Key).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>User-ID des Absenders.</summary>
    public required string SenderId { get; init; }

    /// <summary>Anzeigename des Absenders (Denormalisiert fuer schnellen Read-Zugriff).</summary>
    public required string SenderDisplayName { get; init; }

    /// <summary>
    /// Ziel der Nachricht: entweder ein Raumname (Gruppe) oder eine Empfaenger-User-ID (Direktnachricht).
    /// Wird indiziert, da hierueber die Historie abgefragt wird.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>Art des Ziels - steuert die Zustellung (Gruppe vs. Einzelperson).</summary>
    public required ChatMessageTargetType TargetType { get; init; }

    /// <summary>Inhalt der Nachricht.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// Zeitstempel der Erstellung (UTC). Wird indiziert fuer effiziente
    /// chronologische Historien-Abfragen (ORDER BY Timestamp).
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Art des Nachrichtenziels.</summary>
public enum ChatMessageTargetType
{
    /// <summary>Nachricht an eine Raum-/Gruppen-ID.</summary>
    Room = 0,

    /// <summary>Direktnachricht an eine einzelne User-ID.</summary>
    DirectUser = 1
}
