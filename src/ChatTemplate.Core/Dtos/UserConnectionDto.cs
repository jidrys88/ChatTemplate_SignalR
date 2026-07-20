namespace ChatTemplate.Core.Dtos;

/// <summary>
/// Repraesentiert den Praesenz-Status eines Users, wie er an Clients uebermittelt wird
/// (z.B. bei "UserOnline" / "UserOffline"-Events).
/// </summary>
/// <param name="UserId">Eindeutige User-ID.</param>
/// <param name="IsOnline">Ob der User aktuell mit mind. einer Verbindung online ist.</param>
/// <param name="ConnectionCount">Anzahl aktiver Verbindungen (Multi-Device-Support).</param>
public sealed record UserPresenceDto(string UserId, bool IsOnline, int ConnectionCount);

/// <summary>Request-DTO fuer den Login/Token-Test-Endpoint.</summary>
/// <param name="UserId">Gewuenschte User-ID (in einer echten App: nach Auth-Pruefung ermittelt).</param>
/// <param name="DisplayName">Anzeigename des Users.</param>
public sealed record LoginRequestDto(string UserId, string DisplayName);

/// <summary>Response-DTO mit dem ausgestellten JWT.</summary>
/// <param name="AccessToken">Das signierte JWT.</param>
/// <param name="ExpiresAtUtc">Ablaufzeitpunkt des Tokens (UTC).</param>
public sealed record LoginResponseDto(string AccessToken, DateTimeOffset ExpiresAtUtc);
