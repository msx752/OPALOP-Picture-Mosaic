namespace Opalop.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Opalop.Infrastructure.Persistence;
using Opalop.Infrastructure.Redis;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OpalopDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("PostgreSQL"))
                .UseSnakeCaseNamingConvention());

        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("PostgreSQL")!, name: "postgresql")
            .AddRedis(configuration.GetConnectionString("Redis")!, name: "redis");

        var redisConnection = configuration.GetConnectionString("Redis")!;
        services.AddSingleton(new RedisConnectionManager(redisConnection));

        return services;
    }
}
