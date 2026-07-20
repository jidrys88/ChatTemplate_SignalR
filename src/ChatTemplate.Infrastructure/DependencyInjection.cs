using ChatTemplate.Core.Interfaces;
using ChatTemplate.Infrastructure.Persistence;
using ChatTemplate.Infrastructure.Presence;
using ChatTemplate.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR; // ISignalRServerBuilder, AddStackExchangeRedis-Erweiterung
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChatTemplate.Infrastructure;

/// <summary>
/// Optionen fuer die Datenbank-Konfiguration, gebunden an den Abschnitt
/// "DatabaseSettings" in appsettings.json.
/// </summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    /// <summary>
    /// Schalter zwischen persistenter Datenbank (true, EF Core: Postgres/SQLite)
    /// und der In-Memory-Implementierung fuer Entwicklung/Tests (false).
    /// </summary>
    public bool UsePersistentDatabase { get; init; }

    /// <summary>"Postgres" oder "Sqlite".</summary>
    public string Provider { get; init; } = "Sqlite";

    public string ConnectionString { get; init; } = "Data Source=chat.db";
}

/// <summary>
/// Optionen fuer die SignalR-Skalierung ueber mehrere Server-Instanzen hinweg.
/// Gebunden an den Abschnitt "SignalRScaleOut" in appsettings.json.
/// </summary>
public sealed class SignalRScaleOutSettings
{
    public const string SectionName = "SignalRScaleOut";

    /// <summary>"None", "Redis" oder "AzureSignalR".</summary>
    public string Provider { get; init; } = "None";

    public string? RedisConnectionString { get; init; }

    public string? AzureSignalRConnectionString { get; init; }
}

/// <summary>
/// Zentrale DI-Registrierung fuer alle Infrastructure-Dienste. Wird aus
/// ChatTemplate.API/Program.cs mit einem einzigen Aufruf eingebunden.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>()
                          ?? new DatabaseSettings();

        if (dbSettings.UsePersistentDatabase)
        {
            services.AddDbContext<ChatDbContext>(options =>
            {
                switch (dbSettings.Provider.ToUpperInvariant())
                {
                    case "POSTGRES":
                    case "POSTGRESQL":
                        options.UseNpgsql(dbSettings.ConnectionString);
                        break;
                    case "SQLITE":
                    default:
                        options.UseSqlite(dbSettings.ConnectionString);
                        break;
                }
            });

            services.AddScoped<IChatRepository, EfChatRepository>();
        }
        else
        {
            // In-Memory-Repository muss als Singleton registriert werden,
            // damit die Nachrichten ueber mehrere Requests/Connections hinweg erhalten bleiben.
            services.AddSingleton<IChatRepository, InMemoryChatRepository>();
        }

        // PresenceTracker ist grundsaetzlich ein In-Process-Singleton (siehe Klassendokumentation
        // fuer Hinweise zur horizontalen Skalierung via Redis).
        services.AddSingleton<IPresenceTracker, PresenceTracker>();

        return services;
    }

    /// <summary>
    /// Bereitet SignalR fuer horizontale Skalierung vor (Redis-Backplane oder Azure SignalR Service).
    /// Wird aus Program.cs im Anschluss an services.AddSignalR() aufgerufen:
    /// <c>services.AddSignalR().AddChatScaleOut(configuration)</c>
    /// </summary>
    public static ISignalRServerBuilder AddChatScaleOut(
        this ISignalRServerBuilder signalRBuilder,
        IConfiguration configuration)
    {
        var scaleOutSettings = configuration.GetSection(SignalRScaleOutSettings.SectionName)
            .Get<SignalRScaleOutSettings>() ?? new SignalRScaleOutSettings();

        switch (scaleOutSettings.Provider.ToUpperInvariant())
        {
            case "REDIS":
                if (!string.IsNullOrWhiteSpace(scaleOutSettings.RedisConnectionString))
                {
                    signalRBuilder.AddStackExchangeRedis(scaleOutSettings.RedisConnectionString, options =>
                    {
                        options.Configuration.ChannelPrefix =
                            StackExchange.Redis.RedisChannel.Literal("ChatTemplate");
                    });
                }
                break;

            case "AZURESIGNALR":
                // Hinweis: Erfordert das NuGet-Paket "Microsoft.Azure.SignalR" im API-Projekt.
                // signalRBuilder.Services.AddSignalR().AddAzureSignalR(scaleOutSettings.AzureSignalRConnectionString);
                // Bewusst auskommentiert, um die Kern-Abhaengigkeiten des Templates schlank zu halten.
                break;

            case "NONE":
            default:
                // Keine Skalierung noetig - einzelne Instanz, Standard-In-Memory-Backplane.
                break;
        }

        return signalRBuilder;
    }
}
