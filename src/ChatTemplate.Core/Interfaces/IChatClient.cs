using ChatTemplate.Core.Dtos;

namespace ChatTemplate.Core.Interfaces;

/// <summary>
/// Stark typisierte Definition der Methoden, die der Server auf verbundenen
/// Clients aufrufen kann. Wird von Hub&lt;IChatClient&gt; und
/// IHubContext&lt;ChatHub, IChatClient&gt; genutzt, um Tippfehler in Methodennamen
/// (klassisch "magic strings" bei SignalR) zur Compile-Zeit zu verhindern.
/// </summary>
public interface IChatClient
{
    /// <summary>Wird aufgerufen, wenn eine neue Nachricht (Gruppe oder Direkt) eintrifft.</summary>
    Task ReceiveMessage(ChatMessageDto message);

    /// <summary>Informiert Clients, dass ein User online gegangen ist.</summary>
    Task UserOnline(UserPresenceDto presence);

    /// <summary>Informiert Clients, dass ein User offline gegangen ist.</summary>
    Task UserOffline(UserPresenceDto presence);

    /// <summary>Bestaetigt einem Client den erfolgreichen Beitritt zu einem Raum.</summary>
    Task JoinedRoom(string roomId);

    /// <summary>Bestaetigt einem Client das Verlassen eines Raums.</summary>
    Task LeftRoom(string roomId);

    /// <summary>
    /// Uebermittelt einen einzelnen Historien-Eintrag waehrend des Streamings
    /// (optional zusaetzlich zum IAsyncEnumerable-Rueckgabewert, falls Push-Semantik gewuenscht ist).
    /// </summary>
    Task ReceiveHistoryChunk(ChatMessageDto message);

    /// <summary>Uebermittelt eine fachliche Fehlermeldung an den aufrufenden Client.</summary>
    Task ReceiveError(string errorMessage);
}
