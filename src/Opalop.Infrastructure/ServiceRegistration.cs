namespace Opalop.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Opalop.Application.Interfaces;
using Opalop.Infrastructure.Persistence;
using Opalop.Infrastructure.Redis;
using Opalop.Infrastructure.Storage;

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
        services.AddSingleton<IColorIndex, RedisColorIndex>();
        services.AddSingleton<IMosaicQueue, RedisMosaicQueue>();
        services.AddSingleton<IJobTracker, RedisJobTracker>();

        var minioEndpoint = configuration["MinIO:Endpoint"]!;
        var minioAccessKey = configuration["MinIO:AccessKey"]!;
        var minioSecretKey = configuration["MinIO:SecretKey"]!;
        var minioUseSsl = bool.Parse(configuration["MinIO:UseSSL"] ?? "false");

        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(minioEndpoint)
            .WithCredentials(minioAccessKey, minioSecretKey)
            .WithSSL(minioUseSsl)
            .Build());
        services.AddSingleton<IPhotoStorage, MinioPhotoStorage>();

        return services;
    }
}
