using Koornik.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Koornik.Infrastructure.Data;

public class KoornikDbContext(DbContextOptions<KoornikDbContext> options) : DbContext(options)
{
    public DbSet<Zone> Zones { get; set; }
    public DbSet<Transition> Transitions { get; set; }
    public DbSet<Device> Devices { get; set; }
    
    
}