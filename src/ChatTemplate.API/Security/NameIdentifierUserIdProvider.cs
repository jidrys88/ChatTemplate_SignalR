using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ChatTemplate.API.Security;

/// <summary>
/// Legt fest, wie SignalR die logische "User-ID" einer Connection ableitet.
/// Diese ID wird von <c>Clients.User(userId)</c> verwendet, um Direktnachrichten
/// gezielt an alle Verbindungen eines Users zuzustellen (Multi-Device-faehig).
/// Standardmaessig nutzt SignalR den ClaimTypes.NameIdentifier-Claim - hier explizit
/// gemacht, damit die Quelle klar ersichtlich und leicht austauschbar ist.
/// </summary>
public sealed class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
