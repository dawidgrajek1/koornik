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

        // Add composite index for efficient zone state queries (latest snapshot per zone)
        modelBuilder.Entity<ZoneStateSnapshot>()
            .HasIndex(s => new { s.ZoneId, s.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ZoneStateSnapshots_ZoneId_CreatedAt_Desc");
    }
}