using Koornik.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Koornik.Infrastructure.Data;

public class KoornikDbContext(DbContextOptions<KoornikDbContext> options) : DbContext(options)
{
    public DbSet<Zone> Zones { get; set; }
    public DbSet<Transition> Transitions { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<ZoneStateSnapshot> ZoneStateSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Device entity - zone IDs are optional for border devices
        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(d => d.FrontZoneId).IsRequired(false);
            entity.Property(d => d.BackZoneId).IsRequired(false);
        });

        // Configure Transition entity - zone IDs are optional for border transitions
        modelBuilder.Entity<Transition>(entity =>
        {
            entity.Property(t => t.FromZone).IsRequired(false);
            entity.Property(t => t.ToZone).IsRequired(false);
        });

        // Add composite index for efficient zone state queries (latest snapshot per zone)
        modelBuilder.Entity<ZoneStateSnapshot>()
            .HasIndex(s => new { s.ZoneId, s.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ZoneStateSnapshots_ZoneId_CreatedAt_Desc");
    }
}