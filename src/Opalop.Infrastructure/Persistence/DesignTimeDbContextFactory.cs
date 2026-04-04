namespace Opalop.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpalopDbContext>
{
    public OpalopDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OpalopDbContext>()
            .UseNpgsql("Host=localhost;Database=opalop;Username=opalop;Password=dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new OpalopDbContext(options);
    }
}
