using ChatTemplate.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatTemplate.Infrastructure.Persistence;

/// <summary>
/// EF-Core DbContext fuer die persistente Datenhaltung. Funktioniert provider-agnostisch
/// mit PostgreSQL (Produktion) und SQLite (Entwicklung/lokale Tests) - der konkrete
/// Provider wird bei der Registrierung in Program.cs / DependencyInjection.cs festgelegt.
/// </summary>
/// <remarks>
/// C# 13 Primary Constructor: DbContextOptions werden direkt als Konstruktorparameter
/// entgegengenommen, ohne expliziten Konstruktor-Body.
/// </remarks>
public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    public DbSet<ChatRoom> Rooms => Set<ChatRoom>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(m => m.Id);

            entity.Property(m => m.SenderId).HasMaxLength(128).IsRequired();
            entity.Property(m => m.SenderDisplayName).HasMaxLength(256).IsRequired();
            entity.Property(m => m.TargetId).HasMaxLength(128).IsRequired();
            entity.Property(m => m.Content).HasMaxLength(4000).IsRequired();

            // Wichtig fuer Performance: Historien-Abfragen filtern immer nach TargetId
            // und sortieren nach Timestamp -> zusammengesetzter Index deckt beide Faelle ab.
            entity.HasIndex(m => new { m.TargetId, m.Timestamp })
                  .HasDatabaseName("IX_ChatMessages_TargetId_Timestamp");
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("ChatRooms");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).HasMaxLength(256).IsRequired();
        });
    }
}
