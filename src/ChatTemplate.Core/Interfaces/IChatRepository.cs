using ChatTemplate.Core.Entities;

namespace ChatTemplate.Core.Interfaces;

/// <summary>
/// Abstraktion der Datenhaltung fuer Chat-Nachrichten. Wird sowohl von einer
/// persistenten EF-Core-Implementierung (Produktion) als auch von einer
/// In-Memory-Implementierung (Entwicklung/Tests) implementiert - siehe
/// ChatTemplate.Infrastructure sowie appsettings.json ("DatabaseSettings:UsePersistentDatabase").
/// </summary>
public interface IChatRepository
{
    /// <summary>Persistiert eine neue Chat-Nachricht.</summary>
    Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liefert die Nachrichten-Historie zu einem Ziel (Raum-ID oder User-ID) als
    /// asynchronen Stream, um grosse Historien speicherschonend an Clients zu uebertragen
    /// (siehe IChatClient / ChatHub.GetMessageHistoryStreamAsync).
    /// </summary>
    /// <param name="targetId">Raum-ID oder User-ID, deren Verlauf abgefragt wird.</param>
    /// <param name="take">Maximale Anzahl an Nachrichten (neueste zuerst geladen, dann chronologisch gestreamt).</param>
    IAsyncEnumerable<ChatMessage> StreamMessageHistoryAsync(
        string targetId,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Legt einen Chat-Raum an, falls er noch nicht existiert (idempotent).</summary>
    Task EnsureRoomExistsAsync(string roomId, CancellationToken cancellationToken = default);
}
