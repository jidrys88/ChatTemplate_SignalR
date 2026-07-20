using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ChatTemplate.Core.Entities;
using ChatTemplate.Core.Interfaces;

namespace ChatTemplate.Infrastructure.Repositories;

/// <summary>
/// Thread-sichere In-Memory-Implementierung von <see cref="IChatRepository"/> fuer
/// lokale Entwicklung und Tests, ohne eine echte Datenbank zu benoetigen.
/// Wird verwendet, wenn "DatabaseSettings:UsePersistentDatabase" = false in appsettings.json.
/// Muss als Singleton registriert werden, damit der Zustand ueber Requests hinweg erhalten bleibt.
/// </summary>
public sealed class InMemoryChatRepository : IChatRepository
{
    // ConcurrentBag pro Ziel-ID (Raum oder User), damit paralleles Schreiben lock-frei moeglich ist.
    private readonly ConcurrentDictionary<string, ConcurrentBag<ChatMessage>> _messagesByTarget = new();
    private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new();

    public Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        var bag = _messagesByTarget.GetOrAdd(message.TargetId, static _ => []);
        bag.Add(message);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ChatMessage> StreamMessageHistoryAsync(
        string targetId,
        int take = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_messagesByTarget.TryGetValue(targetId, out var bag))
        {
            yield break;
        }

        // Sortierte Snapshot-Kopie, damit parallele Schreibvorgaenge die Enumeration nicht stoeren.
        var ordered = bag
            .OrderByDescending(m => m.Timestamp)
            .Take(take)
            .OrderBy(m => m.Timestamp)
            .ToList();

        foreach (var message in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simuliert asynchrones I/O, um das Streaming-Verhalten realistisch nachzubilden
            // (z.B. Chunk-weises Nachladen bei sehr grossen Historien).
            await Task.Yield();
            yield return message;
        }
    }

    public Task EnsureRoomExistsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        _rooms.TryAdd(roomId, new ChatRoom { Id = roomId, Name = roomId });
        return Task.CompletedTask;
    }
}
