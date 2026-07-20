using ChatTemplate.Core.Entities;

namespace ChatTemplate.Core.Dtos;

/// <summary>
/// Daten-Transfer-Objekt fuer Chat-Nachrichten, wie es ueber SignalR an Clients
/// gesendet wird. C# 13 Primary-Constructor-artiger "record"-Stil fuer knappe Definition.
/// </summary>
/// <param name="Id">Eindeutige Nachrichten-ID.</param>
/// <param name="SenderId">User-ID des Absenders.</param>
/// <param name="SenderDisplayName">Anzeigename des Absenders.</param>
/// <param name="TargetId">Raum-ID oder Empfaenger-User-ID.</param>
/// <param name="TargetType">Gruppe oder Direktnachricht.</param>
/// <param name="Content">Nachrichtentext.</param>
/// <param name="Timestamp">Erstellungszeitpunkt (UTC).</param>
public sealed record ChatMessageDto(
    Guid Id,
    string SenderId,
    string SenderDisplayName,
    string TargetId,
    ChatMessageTargetType TargetType,
    string Content,
    DateTimeOffset Timestamp)
{
    /// <summary>Erstellt ein DTO aus der Domaenen-Entitaet (Mapping-Hilfsfunktion).</summary>
    public static ChatMessageDto FromEntity(ChatMessage entity) => new(
        entity.Id,
        entity.SenderId,
        entity.SenderDisplayName,
        entity.TargetId,
        entity.TargetType,
        entity.Content,
        entity.Timestamp);
}
