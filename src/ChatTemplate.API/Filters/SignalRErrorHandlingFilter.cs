using System.Diagnostics;
using ChatTemplate.Core.Exceptions;
using Microsoft.AspNetCore.SignalR;

namespace ChatTemplate.API.Filters;

/// <summary>
/// Globaler Hub-Filter, der jeden Hub-Methodenaufruf umschliesst und Exceptions
/// einheitlich behandelt:
///  - Bekannte Fachfehler (<see cref="ChatDomainException"/>) werden 1:1 als
///    <see cref="HubException"/> mit Klartext-Nachricht an den Client weitergereicht,
///    da HubException-Nachrichten (im Gegensatz zu allen anderen Exceptions) an den
///    Client uebertragen werden.
///  - Unerwartete Systemfehler werden NICHT im Detail an den Client weitergegeben
///    (Sicherheitsrisiko: Stacktraces/interne Details), sondern mit einer TraceId
///    geloggt und der Client erhaelt nur eine generische Fehlermeldung inkl. dieser TraceId,
///    damit Support-Anfragen zugeordnet werden koennen.
/// </summary>
/// <remarks>
/// Registrierung in Program.cs:
/// <code>
/// services.AddSignalR(options =>
/// {
///     options.AddFilter&lt;SignalRErrorHandlingFilter&gt;();
/// });
/// </code>
/// C# 13 Primary Constructor fuer den injizierten Logger.
/// </remarks>
public sealed class SignalRErrorHandlingFilter(ILogger<SignalRErrorHandlingFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (ChatDomainException domainException)
        {
            // Fachfehler: bewusst mit Klartext an den Client, damit die UI reagieren kann
            // (z.B. "Der Chat-Raum 'xyz' wurde nicht gefunden.").
            logger.LogWarning(
                domainException,
                "Fachlicher Fehler in Hub-Methode {HubMethod} fuer Connection {ConnectionId}",
                invocationContext.HubMethodName,
                invocationContext.Context.ConnectionId);

            throw new HubException(domainException.Message);
        }
        catch (HubException)
        {
            // Bereits eine bewusst geworfene HubException (z.B. aus einer anderen Middleware) -> durchreichen.
            throw;
        }
        catch (Exception unexpectedException)
        {
            // Unerwarteter Systemfehler: TraceId generieren, vollstaendig serverseitig loggen,
            // Client bekommt NUR die TraceId zur sicheren Fehlerkorrelation.
            var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

            logger.LogError(
                unexpectedException,
                "Unerwarteter Fehler in Hub-Methode {HubMethod} fuer Connection {ConnectionId}. TraceId: {TraceId}",
                invocationContext.HubMethodName,
                invocationContext.Context.ConnectionId,
                traceId);

            throw new HubException($"Ein unerwarteter Fehler ist aufgetreten. Referenz-ID: {traceId}");
        }
    }

    /// <summary>Umschliesst OnConnectedAsync ebenfalls mit einheitlicher Fehlerbehandlung.</summary>
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
            logger.LogError(ex,
                "Fehler in OnConnectedAsync fuer Connection {ConnectionId}. TraceId: {TraceId}",
                context.Context.ConnectionId, traceId);
            throw new HubException($"Verbindungsaufbau fehlgeschlagen. Referenz-ID: {traceId}");
        }
    }

    /// <summary>Umschliesst OnDisconnectedAsync mit Fehlerbehandlung (loggt nur, wirft nicht erneut).</summary>
    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        try
        {
            await next(context, exception);
        }
        catch (Exception ex)
        {
            // Beim Disconnect KEINE Exception mehr an den (bereits getrennten) Client werfen -
            // nur serverseitig loggen, damit Cleanup-Vorgaenge nicht kaskadierend fehlschlagen.
            logger.LogError(ex,
                "Fehler in OnDisconnectedAsync fuer Connection {ConnectionId}",
                context.Context.ConnectionId);
        }
    }
}
