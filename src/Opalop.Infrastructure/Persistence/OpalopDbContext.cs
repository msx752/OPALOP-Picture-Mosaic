namespace Opalop.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Opalop.Domain.Entities;

public class OpalopDbContext(DbContextOptions<OpalopDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<MosaicJob> MosaicJobs => Set<MosaicJob>();
    public DbSet<SocialConnection> SocialConnections => Set<SocialConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpalopDbContext).Assembly);
    }
}
