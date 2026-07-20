namespace ChatTemplate.Core.Entities;

/// <summary>
/// Repraesentiert einen Chat-Raum (Gruppe). Aktuell primaer als logische Gruppierung
/// fuer SignalR-Groups verwendet; kann bei Bedarf um Metadaten (Owner, Settings, ...) erweitert werden.
/// </summary>
public sealed class ChatRoom
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
