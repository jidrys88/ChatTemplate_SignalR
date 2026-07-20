namespace ChatTemplate.Core.Interfaces;

/// <summary>
/// Thread-sicherer Tracker fuer den Online/Offline-Status von Usern.
/// Unterstuetzt Multi-Device-Szenarien: ein User gilt so lange als "online",
/// wie mindestens eine aktive SignalR-Connection fuer seine UserId existiert.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>
    /// Registriert eine neue Verbindung fuer den User.
    /// </summary>
    /// <returns>true, wenn dies die ERSTE Verbindung des Users ist (Uebergang offline -&gt; online).</returns>
    Task<bool> UserConnectedAsync(string userId, string connectionId);

    /// <summary>
    /// Entfernt eine Verbindung des Users (z.B. bei Tab-Schluss / Disconnect).
    /// </summary>
    /// <returns>true, wenn dies die LETZTE Verbindung des Users war (Uebergang online -&gt; offline).</returns>
    Task<bool> UserDisconnectedAsync(string userId, string connectionId);

    /// <summary>Liefert alle aktuell online befindlichen User-IDs.</summary>
    Task<IReadOnlyCollection<string>> GetOnlineUsersAsync();

    /// <summary>Prueft, ob ein bestimmter User online ist.</summary>
    Task<bool> IsUserOnlineAsync(string userId);

    /// <summary>Liefert alle aktiven Connection-IDs eines Users (Multi-Device).</summary>
    Task<IReadOnlyCollection<string>> GetConnectionsForUserAsync(string userId);
}
