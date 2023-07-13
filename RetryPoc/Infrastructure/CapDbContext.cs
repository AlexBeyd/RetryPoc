using Microsoft.EntityFrameworkCore;
using RetryPoc.Application.Models;

namespace RetryPoc.Infrastructure;

public class CapDbContext : DbContext, ICapDbContext
{
    IConfiguration _config;

    public virtual DbSet<PendingEventObject> PendingEvents { get; set; } = null!;

    public CapDbContext(DbContextOptions<CapDbContext> options, IConfiguration config) : base(options)
    {
        _config = config;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PendingEventObject>(e => e.HasKey(e => e.Id));

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_config.GetValue<string>("Cap:PostgreSqlConnectionString"));
        }
    }

    public override int SaveChanges()
    {
        return base.SaveChanges();
    }
}
