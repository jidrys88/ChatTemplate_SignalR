using System.Net.Http.Json;
using ChatTemplate.Core.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

// ---------------------------------------------------------------------------
// ChatTemplate Console-Client
// Demonstriert: JWT-Beschaffung, HubConnectionBuilder mit AutomaticReconnect,
// stark typisierte Event-Handler sowie das Abfangen von HubException.
// ---------------------------------------------------------------------------

Console.Write("API Basis-URL [https://localhost:5001]: ");
var apiBase = ReadLineOrDefault("https://localhost:5001");

Console.Write("Deine User-ID [console-user]: ");
var userId = ReadLineOrDefault("console-user");

Console.Write("Anzeigename [ConsoleUser]: ");
var displayName = ReadLineOrDefault("ConsoleUser");

// ---------------------------------------------------------------------------
// 1) JWT vom AuthController beschaffen (Test-Endpoint, siehe AuthController.cs)
// ---------------------------------------------------------------------------
using var httpClient = new HttpClient { BaseAddress = new Uri(apiBase) };

var loginResponse = await httpClient.PostAsJsonAsync(
    "/api/auth/token",
    new LoginRequestDto(userId, displayName));

loginResponse.EnsureSuccessStatusCode();
var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>()
    ?? throw new InvalidOperationException("Konnte kein Token vom Server beziehen.");

Console.WriteLine($"Token erhalten, gueltig bis {loginResult.ExpiresAtUtc:O}");

// ---------------------------------------------------------------------------
// 2) HubConnection mit automatischem Reconnect aufbauen
// ---------------------------------------------------------------------------
var connection = new HubConnectionBuilder()
    .WithUrl($"{apiBase}/hubs/chat", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult<string?>(loginResult.AccessToken);
    })
    .WithAutomaticReconnect() // Standard-Retry-Intervalle: 0s, 2s, 10s, 30s
    .Build();

// Stark typisierte Registrierung der Server-&gt;Client-Methoden (siehe IChatClient).
connection.On<ChatMessageDto>("ReceiveMessage", message =>
{
    var marker = message.SenderId == userId ? "(ich)" : "";
    Console.WriteLine($"[{message.Timestamp:HH:mm:ss}] {message.SenderDisplayName}{marker}: {message.Content}");
});

connection.On<UserPresenceDto>("UserOnline", presence =>
    Console.WriteLine($"--- {presence.UserId} ist online gegangen ---"));

connection.On<UserPresenceDto>("UserOffline", presence =>
    Console.WriteLine($"--- {presence.UserId} ist offline gegangen ---"));

connection.On<string>("JoinedRoom", roomId =>
    Console.WriteLine($"--- Raum '{roomId}' beigetreten ---"));

connection.On<string>("ReceiveError", errorMessage =>
    Console.WriteLine($"[FEHLER VOM SERVER] {errorMessage}"));

connection.Reconnecting += error =>
{
    Console.WriteLine($"Verbindung unterbrochen, versuche erneut zu verbinden... ({error?.Message})");
    return Task.CompletedTask;
};

connection.Reconnected += connectionId =>
{
    Console.WriteLine($"Wiederverbunden (ConnectionId: {connectionId})");
    return Task.CompletedTask;
};

connection.Closed += error =>
{
    Console.WriteLine($"Verbindung endgueltig geschlossen: {error?.Message}");
    return Task.CompletedTask;
};

try
{
    await connection.StartAsync();
    Console.WriteLine("Verbunden mit dem Chat-Hub.");
}
catch (Exception ex)
{
    Console.WriteLine($"Verbindungsaufbau fehlgeschlagen: {ex.Message}");
    return;
}

Console.WriteLine();
Console.WriteLine("Befehle:");
Console.WriteLine("  /join <raum>            - Raum beitreten");
Console.WriteLine("  /leave <raum>           - Raum verlassen");
Console.WriteLine("  /dm <userId> <text>     - Direktnachricht senden");
Console.WriteLine("  /history <ziel>         - Historie streamen");
Console.WriteLine("  /msg <raum> <text>      - Gruppennachricht senden");
Console.WriteLine("  /quit                   - Beenden");
Console.WriteLine();

while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    try
    {
        if (line.StartsWith("/quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }
        else if (line.StartsWith("/join ", StringComparison.OrdinalIgnoreCase))
        {
            await connection.InvokeAsync("JoinRoom", line["/join ".Length..].Trim());
        }
        else if (line.StartsWith("/leave ", StringComparison.OrdinalIgnoreCase))
        {
            await connection.InvokeAsync("LeaveRoom", line["/leave ".Length..].Trim());
        }
        else if (line.StartsWith("/dm ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line["/dm ".Length..].Split(' ', 2);
            if (parts.Length == 2)
            {
                await connection.InvokeAsync("SendPrivateMessage", parts[0], parts[1]);
            }
        }
        else if (line.StartsWith("/history ", StringComparison.OrdinalIgnoreCase))
        {
            var targetId = line["/history ".Length..].Trim();

            // Konsum eines IAsyncEnumerable&lt;T&gt;-Streams client-seitig via StreamAsync.
            await foreach (var messageDto in connection.StreamAsync<ChatMessageDto>(
                "GetMessageHistoryStreamAsync", targetId, 100))
            {
                Console.WriteLine($"[Historie {messageDto.Timestamp:HH:mm:ss}] {messageDto.SenderDisplayName}: {messageDto.Content}");
            }
        }
        else if (line.StartsWith("/msg ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line["/msg ".Length..].Split(' ', 2);
            if (parts.Length == 2)
            {
                await connection.InvokeAsync("SendGroupMessage", parts[0], parts[1]);
            }
        }
        else
        {
            Console.WriteLine("Unbekannter Befehl.");
        }
    }
    catch (HubException hubException)
    {
        // Fachfehler, die vom SignalRErrorHandlingFilter serverseitig als
        // HubException geworfen wurden, landen hier mit Klartext-Nachricht.
        Console.WriteLine($"[Fachlicher Fehler] {hubException.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Unerwarteter Client-Fehler] {ex.Message}");
    }
}

await connection.StopAsync();
return;

static string ReadLineOrDefault(string defaultValue)
{
    var input = Console.ReadLine();
    return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
}
