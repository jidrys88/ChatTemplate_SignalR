using System.Runtime.CompilerServices;
using System.Security.Claims;
using ChatTemplate.Core.Dtos;
using ChatTemplate.Core.Entities;
using ChatTemplate.Core.Exceptions;
using ChatTemplate.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatTemplate.API.Hubs;

/// <summary>
/// Zentraler Chat-Hub. Stark typisiert ueber <see cref="Hub{IChatClient}"/>, sodass
/// alle Aufrufe an Clients (this.Clients.Caller.ReceiveMessage(...) etc.) compile-time-sicher sind.
/// Alle oeffentlichen Methoden erfordern eine authentifizierte Verbindung (JWT, siehe Program.cs).
/// </summary>
/// <remarks>
/// C# 13 Primary Constructor fuer die injizierten Abhaengigkeiten (Repository, PresenceTracker, Logger).
/// </remarks>
[Authorize]
public sealed class ChatHub(
    IChatRepository chatRepository,
    IPresenceTracker presenceTracker,
    ILogger<ChatHub> logger) : Hub<IChatClient>
{
    /// <summary>Ermittelt die User-ID aus den Claims der aktuellen Verbindung.</summary>
    private string CurrentUserId =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedChatActionException("Kein authentifizierter User gefunden.");

    private string CurrentDisplayName =>
        Context.User?.FindFirstValue(ClaimTypes.Name) ?? CurrentUserId;

    /// <summary>
    /// Wird beim Verbindungsaufbau eines Clients ausgefuehrt: registriert die Connection
    /// im PresenceTracker und informiert andere Clients, falls dies die erste Verbindung
    /// des Users ist (Uebergang offline -&gt; online).
    /// Fehlerbehandlung erfolgt zusaetzlich global durch SignalRErrorHandlingFilter.OnConnectedAsync.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;

        var isFirstConnection = await presenceTracker.UserConnectedAsync(userId, Context.ConnectionId);

        logger.LogInformation(
            "User {UserId} verbunden (ConnectionId: {ConnectionId}, ErsteVerbindung: {IsFirst})",
            userId, Context.ConnectionId, isFirstConnection);

        if (isFirstConnection)
        {
            var onlineCount = (await presenceTracker.GetConnectionsForUserAsync(userId)).Count;
            await Clients.Others.UserOnline(new UserPresenceDto(userId, IsOnline: true, onlineCount));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Wird beim Verbindungsabbau ausgefuehrt: entfernt die Connection aus dem PresenceTracker
    /// und informiert andere Clients, falls dies die letzte Verbindung des Users war.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;

        var wasLastConnection = await presenceTracker.UserDisconnectedAsync(userId, Context.ConnectionId);

        logger.LogInformation(
            "User {UserId} getrennt (ConnectionId: {ConnectionId}, LetzteVerbindung: {WasLast}), Grund: {Reason}",
            userId, Context.ConnectionId, wasLastConnection, exception?.Message ?? "regulaer");

        if (wasLastConnection)
        {
            await Clients.Others.UserOffline(new UserPresenceDto(userId, IsOnline: false, ConnectionCount: 0));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Tritt einem Chat-Raum (SignalR-Group) bei. Legt den Raum idempotent an,
    /// falls er noch nicht existiert.
    /// </summary>
    public async Task JoinRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new InvalidMessageException("Raum-ID darf nicht leer sein.");
        }

        await chatRepository.EnsureRoomExistsAsync(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        logger.LogInformation("User {UserId} ist Raum {RoomId} beigetreten", CurrentUserId, roomId);

        await Clients.Caller.JoinedRoom(roomId);
    }

    /// <summary>Verlaesst einen Chat-Raum (SignalR-Group).</summary>
    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        logger.LogInformation("User {UserId} hat Raum {RoomId} verlassen", CurrentUserId, roomId);

        await Clients.Caller.LeftRoom(roomId);
    }

    /// <summary>
    /// Sendet eine Nachricht an alle Mitglieder eines Raums (Gruppe), inklusive des Absenders.
    /// </summary>
    public async Task SendGroupMessage(string roomId, string content)
    {
        ValidateContent(content);

        var message = new ChatMessage
        {
            SenderId = CurrentUserId,
            SenderDisplayName = CurrentDisplayName,
            TargetId = roomId,
            TargetType = ChatMessageTargetType.Room,
            Content = content
        };

        await chatRepository.AddMessageAsync(message);

        var dto = ChatMessageDto.FromEntity(message);
        await Clients.Group(roomId).ReceiveMessage(dto);
    }

    /// <summary>
    /// Sendet eine Direktnachricht an einen bestimmten User (alle dessen aktiven Geraete),
    /// nutzt dazu IUserIdProvider (siehe NameIdentifierUserIdProvider) via Clients.User(...).
    /// Zusaetzlich erhaelt auch der Absender selbst eine Kopie (Clients.Caller), damit die
    /// eigene Chat-Historie in Multi-Device-Szenarien konsistent bleibt.
    /// </summary>
    public async Task SendPrivateMessage(string recipientUserId, string content)
    {
        ValidateContent(content);

        var isRecipientOnline = await presenceTracker.IsUserOnlineAsync(recipientUserId);
        if (!isRecipientOnline)
        {
            // Fachlich bewusst als Warnung behandelt: Nachricht wird dennoch persistiert,
            // damit der Empfaenger sie beim naechsten Login/Verlauf sieht - der Absender wird
            // lediglich informiert, dass keine Echtzeit-Zustellung stattgefunden hat.
            logger.LogInformation("Empfaenger {RecipientId} ist aktuell offline, Nachricht wird dennoch gespeichert.", recipientUserId);
        }

        var message = new ChatMessage
        {
            SenderId = CurrentUserId,
            SenderDisplayName = CurrentDisplayName,
            TargetId = recipientUserId,
            TargetType = ChatMessageTargetType.DirectUser,
            Content = content
        };

        await chatRepository.AddMessageAsync(message);

        var dto = ChatMessageDto.FromEntity(message);

        // Clients.User(...) sendet an ALLE aktiven Connections des Zielbenutzers (Multi-Device).
        await Clients.User(recipientUserId).ReceiveMessage(dto);
        await Clients.Caller.ReceiveMessage(dto);
    }

    /// <summary>
    /// Streamt die Nachrichten-Historie eines Ziels (Raum oder User) effizient an den Client,
    /// ohne die komplette Historie vorher im Speicher zu materialisieren.
    /// SignalR unterstuetzt IAsyncEnumerable&lt;T&gt; als natives Streaming-Rueckgabemuster.
    /// </summary>
    /// <param name="targetId">Raum-ID oder User-ID.</param>
    /// <param name="take">Maximale Anzahl an Nachrichten.</param>
    /// <param name="cancellationToken">
    /// Wird von SignalR automatisch injiziert und abgebrochen, wenn der Client
    /// das Streaming vorzeitig beendet (StreamAsChannelAsync/IAsyncEnumerable-Kompatibilitaet).
    /// </param>
    public async IAsyncEnumerable<ChatMessageDto> GetMessageHistoryStreamAsync(
        string targetId,
        int take = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in chatRepository.StreamMessageHistoryAsync(targetId, take, cancellationToken))
        {
            yield return ChatMessageDto.FromEntity(message);
        }
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidMessageException("Nachrichtentext darf nicht leer sein.");
        }

        if (content.Length > 4000)
        {
            throw new InvalidMessageException("Nachrichtentext ueberschreitet die maximale Laenge von 4000 Zeichen.");
        }
    }
}
