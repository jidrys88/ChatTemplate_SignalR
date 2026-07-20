using System.Runtime.CompilerServices;
using ChatTemplate.Core.Entities;
using ChatTemplate.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChatTemplate.Infrastructure.Persistence;

/// <summary>
/// Produktions-Implementierung von <see cref="IChatRepository"/> auf Basis von EF Core.
/// Wird verwendet, wenn "DatabaseSettings:UsePersistentDatabase" = true in appsettings.json.
/// </summary>
/// <remarks>C# 13 Primary Constructor fuer Dependency Injection des DbContext.</remarks>
public sealed class EfChatRepository(ChatDbContext dbContext) : IChatRepository
{
    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async IAsyncEnumerable<ChatMessage> StreamMessageHistoryAsync(
        string targetId,
        int take = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Nutzt den zusammengesetzten Index (TargetId, Timestamp) aus ChatDbContext.
        // AsAsyncEnumerable() sorgt dafuer, dass Zeilen einzeln vom Datenbank-Cursor
        // gestreamt werden, statt die komplette Historie im Speicher zu materialisieren.
        var query = dbContext.Messages
            .AsNoTracking()
            .Where(m => m.TargetId == targetId)
            .OrderByDescending(m => m.Timestamp)
            .Take(take)
            .OrderBy(m => m.Timestamp) // chronologisch aufsteigend zurueckgeben
            .AsAsyncEnumerable();

        await foreach (var message in query.WithCancellation(cancellationToken))
        {
            yield return message;
        }
    }

    public async Task EnsureRoomExistsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Rooms.AnyAsync(r => r.Id == roomId, cancellationToken);
        if (!exists)
        {
            dbContext.Rooms.Add(new ChatRoom { Id = roomId, Name = roomId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
