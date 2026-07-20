using System.Collections.Concurrent;
using ChatTemplate.Core.Interfaces;

namespace ChatTemplate.Infrastructure.Presence;

/// <summary>
/// Thread-sichere Implementierung von <see cref="IPresenceTracker"/>.
/// Haelt pro User-ID die Menge aktiver Connection-IDs vor, um Multi-Device-Verbindungen
/// korrekt abzubilden: ein User ist online, solange mindestens eine Connection existiert.
/// Muss als Singleton registriert werden (siehe DependencyInjection.cs).
/// </summary>
/// <remarks>
/// In einem horizontal skalierten Szenario (mehrere API-Instanzen) muesste dieser Tracker
/// durch eine verteilte Implementierung (z.B. Redis) ersetzt werden - das Interface
/// IPresenceTracker macht diesen Austausch transparent moeglich.
/// </remarks>
public sealed class PresenceTracker : IPresenceTracker
{
    // Wert ist ein Thread-sicheres Set von Connection-IDs (ConcurrentDictionary als Set-Ersatz).
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _onlineUsers = new();

    public Task<bool> UserConnectedAsync(string userId, string connectionId)
    {
        var connections = _onlineUsers.GetOrAdd(userId, static _ => new ConcurrentDictionary<string, byte>());

        var isFirstConnectionOfUser = connections.IsEmpty;
        connections.TryAdd(connectionId, 0);

        return Task.FromResult(isFirstConnectionOfUser);
    }

    public Task<bool> UserDisconnectedAsync(string userId, string connectionId)
    {
        if (!_onlineUsers.TryGetValue(userId, out var connections))
        {
            return Task.FromResult(false);
        }

        connections.TryRemove(connectionId, out _);

        var wasLastConnectionOfUser = connections.IsEmpty;
        if (wasLastConnectionOfUser)
        {
            // Aufraeumen, damit GetOnlineUsersAsync keine "leeren" User zurueckgibt.
            _onlineUsers.TryRemove(userId, out _);
        }

        return Task.FromResult(wasLastConnectionOfUser);
    }

    public Task<IReadOnlyCollection<string>> GetOnlineUsersAsync()
    {
        IReadOnlyCollection<string> users = _onlineUsers.Keys.ToArray();
        return Task.FromResult(users);
    }

    public Task<bool> IsUserOnlineAsync(string userId) =>
        Task.FromResult(_onlineUsers.TryGetValue(userId, out var connections) && !connections.IsEmpty);

    public Task<IReadOnlyCollection<string>> GetConnectionsForUserAsync(string userId)
    {
        IReadOnlyCollection<string> connectionIds = _onlineUsers.TryGetValue(userId, out var connections)
            ? connections.Keys.ToArray()
            : [];

        return Task.FromResult(connectionIds);
    }
}
