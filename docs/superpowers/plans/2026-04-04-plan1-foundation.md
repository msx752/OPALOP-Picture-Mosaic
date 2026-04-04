# Plan 1: Foundation — Solution Scaffold, Docker, Database, Auth

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the .NET 9 solution skeleton with Docker Compose infrastructure, PostgreSQL schema via EF Core, Keycloak authentication, MinIO integration, and health checks — so all subsequent plans have a working foundation to build on.

**Architecture:** Modular monolith with 6 projects (Domain, Application, Infrastructure, Mosaic.Engine, Api, Worker). Clean Architecture with inward-pointing dependencies. Infrastructure services (Redis, PostgreSQL, MinIO, Keycloak) run in Docker Compose.

**Tech Stack:** .NET 9, EF Core 9 + Npgsql, StackExchange.Redis, Minio SDK, Keycloak (JWT Bearer), Docker Compose, xUnit + Testcontainers

**Spec reference:** `docs/superpowers/specs/2026-04-04-opalop-net9-migration-design.md`

---

## File Map

```
OPALOP-Picture-Mosaic/
├── src/
│   ├── Opalop.Domain/
│   │   ├── Opalop.Domain.csproj
│   │   └── Entities/
│   │       ├── User.cs
│   │       ├── Photo.cs
│   │       ├── Resource.cs
│   │       ├── MosaicJob.cs
│   │       └── SocialConnection.cs
│   │   └── ValueObjects/
│   │       ├── ColorFingerprint.cs
│   │       ├── QuadrantLab.cs
│   │       └── PixFormat.cs
│   │   └── Enums/
│   │       ├── JobStatus.cs
│   │       ├── PhotoSource.cs
│   │       └── SocialProvider.cs
│   │
│   ├── Opalop.Application/
│   │   ├── Opalop.Application.csproj
│   │   └── Interfaces/
│   │       ├── IPhotoStorage.cs
│   │       ├── IColorIndex.cs
│   │       ├── IMosaicQueue.cs
│   │       └── IJobTracker.cs
│   │
│   ├── Opalop.Infrastructure/
│   │   ├── Opalop.Infrastructure.csproj
│   │   └── Persistence/
│   │       ├── OpalopDbContext.cs
│   │       ├── Configurations/
│   │       │   ├── UserConfiguration.cs
│   │       │   ├── PhotoConfiguration.cs
│   │       │   ├── ResourceConfiguration.cs
│   │       │   ├── MosaicJobConfiguration.cs
│   │       │   └── SocialConnectionConfiguration.cs
│   │       └── DesignTimeDbContextFactory.cs
│   │   └── Storage/
│   │       └── MinioPhotoStorage.cs
│   │   └── ServiceRegistration.cs
│   │
│   ├── Opalop.Mosaic.Engine/
│   │   └── Opalop.Mosaic.Engine.csproj          (empty placeholder for Plan 2)
│   │
│   ├── Opalop.Api/
│   │   ├── Opalop.Api.csproj
│   │   ├── Program.cs
│   │   ├── Dockerfile
│   │   └── appsettings.json
│   │
│   └── Opalop.Worker/
│       ├── Opalop.Worker.csproj
│       ├── Program.cs
│       └── Dockerfile
│
├── tests/
│   ├── Opalop.Domain.Tests/
│   │   ├── Opalop.Domain.Tests.csproj
│   │   └── ValueObjects/
│   │       └── ColorFingerprintTests.cs
│   └── Opalop.Infrastructure.Tests/
│       ├── Opalop.Infrastructure.Tests.csproj
│       └── Persistence/
│           └── OpalopDbContextTests.cs
│
├── infra/
│   ├── keycloak/
│   │   └── realm-export.json
│   └── minio/
│       └── init-buckets.sh
│
├── docker-compose.yml
├── docker-compose.override.yml
├── Opalop.sln
├── Directory.Build.props
├── Directory.Packages.props
└── .editorconfig
```

---

### Task 1: Solution Scaffold & Build Configuration

**Files:**
- Create: `Opalop.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `src/Opalop.Domain/Opalop.Domain.csproj`
- Create: `src/Opalop.Application/Opalop.Application.csproj`
- Create: `src/Opalop.Infrastructure/Opalop.Infrastructure.csproj`
- Create: `src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj`
- Create: `src/Opalop.Api/Opalop.Api.csproj`
- Create: `src/Opalop.Worker/Opalop.Worker.csproj`
- Create: `tests/Opalop.Domain.Tests/Opalop.Domain.Tests.csproj`
- Create: `tests/Opalop.Infrastructure.Tests/Opalop.Infrastructure.Tests.csproj`

- [ ] **Step 1: Create Directory.Build.props**

This sets shared build properties for all projects in the solution.

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create Directory.Packages.props**

Central package management — all NuGet versions in one place.

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- EF Core + PostgreSQL -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.3" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />
    <PackageVersion Include="EFCore.NamingConventions" Version="9.0.0" />
    <!-- Redis -->
    <PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
    <!-- MinIO -->
    <PackageVersion Include="Minio" Version="6.0.4" />
    <!-- Auth -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.3" />
    <!-- SignalR -->
    <PackageVersion Include="Microsoft.AspNetCore.SignalR" Version="1.2.0" />
    <!-- Swagger -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="7.2.0" />
    <!-- Image processing (placeholder for Plan 2) -->
    <PackageVersion Include="SkiaSharp" Version="3.116.1" />
    <!-- Testing -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.3.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="4.3.0" />
    <PackageVersion Include="FluentAssertions" Version="7.1.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create .editorconfig**

```ini
# .editorconfig
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{csproj,props,xml,json,yml,yaml}]
indent_size = 2

[*.cs]
dotnet_sort_system_directives_first = true
csharp_using_directive_placement = outside_namespace
csharp_style_namespace_declarations = file_scoped:warning
```

- [ ] **Step 4: Create all .csproj files**

Domain (zero dependencies):
```xml
<!-- src/Opalop.Domain/Opalop.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

Application (depends on Domain):
```xml
<!-- src/Opalop.Application/Opalop.Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Opalop.Domain\Opalop.Domain.csproj" />
  </ItemGroup>
</Project>
```

Infrastructure (depends on Domain + Application):
```xml
<!-- src/Opalop.Infrastructure/Opalop.Infrastructure.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Opalop.Domain\Opalop.Domain.csproj" />
    <ProjectReference Include="..\Opalop.Application\Opalop.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="StackExchange.Redis" />
    <PackageReference Include="Minio" />
  </ItemGroup>
</Project>
```

Mosaic.Engine placeholder (depends on Domain only):
```xml
<!-- src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Opalop.Domain\Opalop.Domain.csproj" />
  </ItemGroup>
</Project>
```

Api (web project):
```xml
<!-- src/Opalop.Api/Opalop.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\Opalop.Application\Opalop.Application.csproj" />
    <ProjectReference Include="..\Opalop.Infrastructure\Opalop.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Swashbuckle.AspNetCore" />
  </ItemGroup>
</Project>
```

Worker:
```xml
<!-- src/Opalop.Worker/Opalop.Worker.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <ItemGroup>
    <ProjectReference Include="..\Opalop.Application\Opalop.Application.csproj" />
    <ProjectReference Include="..\Opalop.Infrastructure\Opalop.Infrastructure.csproj" />
    <ProjectReference Include="..\Opalop.Mosaic.Engine\Opalop.Mosaic.Engine.csproj" />
  </ItemGroup>
</Project>
```

Domain.Tests:
```xml
<!-- tests/Opalop.Domain.Tests/Opalop.Domain.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\Opalop.Domain\Opalop.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>
</Project>
```

Infrastructure.Tests:
```xml
<!-- tests/Opalop.Infrastructure.Tests/Opalop.Infrastructure.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\src\Opalop.Infrastructure\Opalop.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create the solution file and add all projects**

```bash
dotnet new sln -n Opalop -o .
dotnet sln Opalop.sln add src/Opalop.Domain/Opalop.Domain.csproj
dotnet sln Opalop.sln add src/Opalop.Application/Opalop.Application.csproj
dotnet sln Opalop.sln add src/Opalop.Infrastructure/Opalop.Infrastructure.csproj
dotnet sln Opalop.sln add src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj
dotnet sln Opalop.sln add src/Opalop.Api/Opalop.Api.csproj
dotnet sln Opalop.sln add src/Opalop.Worker/Opalop.Worker.csproj
dotnet sln Opalop.sln add tests/Opalop.Domain.Tests/Opalop.Domain.Tests.csproj
dotnet sln Opalop.sln add tests/Opalop.Infrastructure.Tests/Opalop.Infrastructure.Tests.csproj
```

- [ ] **Step 6: Build the solution to verify**

```bash
dotnet build Opalop.sln
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: scaffold .NET 9 solution with 6 projects and 2 test projects"
```

---

### Task 2: Domain Entities & Value Objects

**Files:**
- Create: `src/Opalop.Domain/Enums/JobStatus.cs`
- Create: `src/Opalop.Domain/Enums/PhotoSource.cs`
- Create: `src/Opalop.Domain/Enums/SocialProvider.cs`
- Create: `src/Opalop.Domain/ValueObjects/QuadrantLab.cs`
- Create: `src/Opalop.Domain/ValueObjects/ColorFingerprint.cs`
- Create: `src/Opalop.Domain/ValueObjects/PixFormat.cs`
- Create: `src/Opalop.Domain/Entities/User.cs`
- Create: `src/Opalop.Domain/Entities/Photo.cs`
- Create: `src/Opalop.Domain/Entities/Resource.cs`
- Create: `src/Opalop.Domain/Entities/MosaicJob.cs`
- Create: `src/Opalop.Domain/Entities/SocialConnection.cs`
- Test: `tests/Opalop.Domain.Tests/ValueObjects/ColorFingerprintTests.cs`

- [ ] **Step 1: Write the failing test for ColorFingerprint**

```csharp
// tests/Opalop.Domain.Tests/ValueObjects/ColorFingerprintTests.cs
namespace Opalop.Domain.Tests.ValueObjects;

using FluentAssertions;
using Opalop.Domain.ValueObjects;

public class ColorFingerprintTests
{
    [Fact]
    public void DeltaE_IdenticalFingerprints_ReturnsZero()
    {
        var quad = new QuadrantLab(50.0f, 20.0f, -10.0f);
        var fp = new ColorFingerprint(
            Total: quad,
            TopLeft: quad,
            TopRight: quad,
            BottomLeft: quad,
            BottomRight: quad);

        var result = fp.WeightedDeltaE(fp);

        result.Should().Be(0f);
    }

    [Fact]
    public void DeltaE_DifferentFingerprints_ReturnsPositiveValue()
    {
        var fp1 = new ColorFingerprint(
            Total: new QuadrantLab(50f, 20f, -10f),
            TopLeft: new QuadrantLab(55f, 22f, -8f),
            TopRight: new QuadrantLab(45f, 18f, -12f),
            BottomLeft: new QuadrantLab(52f, 21f, -9f),
            BottomRight: new QuadrantLab(48f, 19f, -11f));

        var fp2 = new ColorFingerprint(
            Total: new QuadrantLab(70f, 10f, 5f),
            TopLeft: new QuadrantLab(75f, 12f, 7f),
            TopRight: new QuadrantLab(65f, 8f, 3f),
            BottomLeft: new QuadrantLab(72f, 11f, 6f),
            BottomRight: new QuadrantLab(68f, 9f, 4f));

        var result = fp1.WeightedDeltaE(fp2);

        result.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void QuadrantLab_DeltaE_CalculatesEuclideanDistance()
    {
        var a = new QuadrantLab(50f, 20f, 30f);
        var b = new QuadrantLab(50f, 20f, 30f);

        a.DeltaE(b).Should().Be(0f);

        var c = new QuadrantLab(53f, 24f, 30f);
        // sqrt(9 + 16 + 0) = 5
        a.DeltaE(c).Should().Be(5f);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Opalop.Domain.Tests --filter "FullyQualifiedName~ColorFingerprintTests" -v n
```

Expected: FAIL — types `QuadrantLab` and `ColorFingerprint` do not exist.

- [ ] **Step 3: Create enums**

```csharp
// src/Opalop.Domain/Enums/JobStatus.cs
namespace Opalop.Domain.Enums;

public enum JobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
```

```csharp
// src/Opalop.Domain/Enums/PhotoSource.cs
namespace Opalop.Domain.Enums;

public enum PhotoSource
{
    Upload,
    Google
}
```

```csharp
// src/Opalop.Domain/Enums/SocialProvider.cs
namespace Opalop.Domain.Enums;

public enum SocialProvider
{
    Google,
    Instagram
}
```

- [ ] **Step 4: Create QuadrantLab value object**

```csharp
// src/Opalop.Domain/ValueObjects/QuadrantLab.cs
namespace Opalop.Domain.ValueObjects;

public readonly record struct QuadrantLab(float L, float A, float B)
{
    public float DeltaE(QuadrantLab other)
    {
        var dL = L - other.L;
        var dA = A - other.A;
        var dB = B - other.B;
        return MathF.Sqrt(dL * dL + dA * dA + dB * dB);
    }
}
```

- [ ] **Step 5: Create ColorFingerprint value object**

```csharp
// src/Opalop.Domain/ValueObjects/ColorFingerprint.cs
namespace Opalop.Domain.ValueObjects;

public readonly record struct ColorFingerprint(
    QuadrantLab Total,
    QuadrantLab TopLeft,
    QuadrantLab TopRight,
    QuadrantLab BottomLeft,
    QuadrantLab BottomRight)
{
    public float WeightedDeltaE(ColorFingerprint other)
    {
        return 0.4f * Total.DeltaE(other.Total)
             + 0.15f * TopLeft.DeltaE(other.TopLeft)
             + 0.15f * TopRight.DeltaE(other.TopRight)
             + 0.15f * BottomLeft.DeltaE(other.BottomLeft)
             + 0.15f * BottomRight.DeltaE(other.BottomRight);
    }
}
```

- [ ] **Step 6: Create PixFormat value object**

```csharp
// src/Opalop.Domain/ValueObjects/PixFormat.cs
namespace Opalop.Domain.ValueObjects;

public readonly record struct PixFormat
{
    public int Size { get; }

    private static readonly int[] ValidSizes = [12, 20, 36, 48, 64, 94];

    private PixFormat(int size) => Size = size;

    public static PixFormat From(int size)
    {
        if (!ValidSizes.Contains(size))
            throw new ArgumentException($"Invalid pixel format: {size}. Valid sizes: {string.Join(", ", ValidSizes)}");
        return new PixFormat(size);
    }

    public static PixFormat Default => new(94);
}
```

- [ ] **Step 7: Create domain entities**

```csharp
// src/Opalop.Domain/Entities/User.cs
namespace Opalop.Domain.Entities;

public class User
{
    public Guid Id { get; init; }
    public required string KeycloakId { get; init; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public int TicketBalance { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public List<Photo> Photos { get; init; } = [];
    public List<Resource> Resources { get; init; } = [];
    public List<MosaicJob> MosaicJobs { get; init; } = [];
    public List<SocialConnection> SocialConnections { get; init; } = [];
}
```

```csharp
// src/Opalop.Domain/Entities/Photo.cs
namespace Opalop.Domain.Entities;

using Opalop.Domain.Enums;
using Opalop.Domain.ValueObjects;

public class Photo
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Filename { get; init; }
    public PhotoSource Source { get; init; }
    public required string StoragePath { get; init; }
    public required string TilePath { get; init; }
    public float TotalL { get; set; }
    public float TotalA { get; set; }
    public float TotalB { get; set; }
    public List<QuadrantLab> Quadrants { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
}
```

```csharp
// src/Opalop.Domain/Entities/Resource.cs
namespace Opalop.Domain.Entities;

public class Resource
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Filename { get; init; }
    public required string StoragePath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
    public List<MosaicJob> MosaicJobs { get; init; } = [];
}
```

```csharp
// src/Opalop.Domain/Entities/MosaicJob.cs
namespace Opalop.Domain.Entities;

using Opalop.Domain.Enums;

public class MosaicJob
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid ResourceId { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int PxFormat { get; init; }
    public int TotalTiles { get; set; }
    public int CompletedTiles { get; set; }
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User User { get; init; } = null!;
    public Resource Resource { get; init; } = null!;
}
```

```csharp
// src/Opalop.Domain/Entities/SocialConnection.cs
namespace Opalop.Domain.Entities;

using Opalop.Domain.Enums;

public class SocialConnection
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public SocialProvider Provider { get; init; }
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
}
```

- [ ] **Step 8: Run tests to verify they pass**

```bash
dotnet test tests/Opalop.Domain.Tests --filter "FullyQualifiedName~ColorFingerprintTests" -v n
```

Expected: 3 tests pass.

- [ ] **Step 9: Commit**

```bash
git add src/Opalop.Domain/ tests/Opalop.Domain.Tests/
git commit -m "feat: add domain entities, value objects, and enums"
```

---

### Task 3: Application Layer Interfaces

**Files:**
- Create: `src/Opalop.Application/Interfaces/IPhotoStorage.cs`
- Create: `src/Opalop.Application/Interfaces/IColorIndex.cs`
- Create: `src/Opalop.Application/Interfaces/IMosaicQueue.cs`
- Create: `src/Opalop.Application/Interfaces/IJobTracker.cs`

- [ ] **Step 1: Create IPhotoStorage**

```csharp
// src/Opalop.Application/Interfaces/IPhotoStorage.cs
namespace Opalop.Application.Interfaces;

public interface IPhotoStorage
{
    Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string path, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string path, int expirySeconds = 3600, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create IColorIndex**

```csharp
// src/Opalop.Application/Interfaces/IColorIndex.cs
namespace Opalop.Application.Interfaces;

using Opalop.Domain.ValueObjects;

public interface IColorIndex
{
    Task AddPhotoAsync(Guid userId, Guid photoId, ColorFingerprint fingerprint, CancellationToken ct = default);
    Task RemovePhotoAsync(Guid userId, Guid photoId, CancellationToken ct = default);
    Task<ColorMatchResult?> FindBestMatchAsync(Guid userId, ColorFingerprint target, Guid jobId, int maxUsagePerPhoto = 5, CancellationToken ct = default);
    Task RebuildIndexAsync(Guid userId, IEnumerable<(Guid PhotoId, ColorFingerprint Fingerprint)> photos, CancellationToken ct = default);
}

public record ColorMatchResult(Guid PhotoId, float DeltaE, ColorFingerprint Fingerprint);
```

- [ ] **Step 3: Create IMosaicQueue**

```csharp
// src/Opalop.Application/Interfaces/IMosaicQueue.cs
namespace Opalop.Application.Interfaces;

using Opalop.Domain.ValueObjects;

public interface IMosaicQueue
{
    Task EnqueueTilesAsync(Guid jobId, IReadOnlyList<TileTask> tiles, CancellationToken ct = default);
    Task<TileTask?> DequeueAsync(string consumerName, CancellationToken ct = default);
    Task AcknowledgeAsync(string messageId, CancellationToken ct = default);
    Task ClaimStaleMessagesAsync(string consumerName, TimeSpan idleTimeout, CancellationToken ct = default);
}

public record TileTask(
    string MessageId,
    Guid JobId,
    int TileIndex,
    int X,
    int Y,
    ColorFingerprint Fingerprint);
```

- [ ] **Step 4: Create IJobTracker**

```csharp
// src/Opalop.Application/Interfaces/IJobTracker.cs
namespace Opalop.Application.Interfaces;

public interface IJobTracker
{
    Task InitJobAsync(Guid jobId, int totalTiles, Guid userId, int pxFormat, CancellationToken ct = default);
    Task<int> IncrementCompletedAsync(Guid jobId, CancellationToken ct = default);
    Task<JobInfo?> GetJobInfoAsync(Guid jobId, CancellationToken ct = default);
    Task SetJobCompletedAsync(Guid jobId, string resultPath, CancellationToken ct = default);
    Task SetJobFailedAsync(Guid jobId, string error, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default);
    Task ReleaseLockAsync(Guid userId, CancellationToken ct = default);
}

public record JobInfo(Guid JobId, int TotalTiles, int CompletedTiles, Guid UserId, int PxFormat, string Status);
```

- [ ] **Step 5: Build to verify**

```bash
dotnet build Opalop.sln
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Opalop.Application/
git commit -m "feat: add application layer interfaces for storage, color index, queue, and job tracking"
```

---

### Task 4: EF Core DbContext & PostgreSQL Configuration

**Files:**
- Create: `src/Opalop.Infrastructure/Persistence/OpalopDbContext.cs`
- Create: `src/Opalop.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Create: `src/Opalop.Infrastructure/Persistence/Configurations/PhotoConfiguration.cs`
- Create: `src/Opalop.Infrastructure/Persistence/Configurations/ResourceConfiguration.cs`
- Create: `src/Opalop.Infrastructure/Persistence/Configurations/MosaicJobConfiguration.cs`
- Create: `src/Opalop.Infrastructure/Persistence/Configurations/SocialConnectionConfiguration.cs`
- Create: `src/Opalop.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
- Test: `tests/Opalop.Infrastructure.Tests/Persistence/OpalopDbContextTests.cs`

- [ ] **Step 1: Write the failing integration test**

```csharp
// tests/Opalop.Infrastructure.Tests/Persistence/OpalopDbContextTests.cs
namespace Opalop.Infrastructure.Tests.Persistence;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

public class OpalopDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private OpalopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OpalopDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        var ctx = new OpalopDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task CanInsertAndQueryUser()
    {
        await using var ctx = CreateContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = "kc-123",
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Users.FirstAsync(u => u.KeycloakId == "kc-123");
        loaded.Email.Should().Be("test@example.com");
        loaded.TicketBalance.Should().Be(100);
    }

    [Fact]
    public async Task CanInsertPhotoWithQuadrants()
    {
        await using var ctx = CreateContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = "kc-456",
            Email = "photo@test.com"
        };
        ctx.Users.Add(user);

        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Filename = "test.jpg",
            Source = PhotoSource.Upload,
            StoragePath = "photos/originals/test.jpg",
            TilePath = "photos/tiles/test.jpg",
            TotalL = 52.3f,
            TotalA = 28.1f,
            TotalB = -14.7f,
            Quadrants =
            [
                new QuadrantLab(61.2f, 32.4f, -10.2f),
                new QuadrantLab(48.9f, 25.7f, -18.3f),
                new QuadrantLab(55.1f, 30.0f, -12.1f),
                new QuadrantLab(44.0f, 24.2f, -18.2f)
            ]
        };
        ctx.Photos.Add(photo);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Photos
            .FirstAsync(p => p.Filename == "test.jpg");
        loaded.TotalL.Should().Be(52.3f);
        loaded.Quadrants.Should().HaveCount(4);
        loaded.Quadrants[0].L.Should().Be(61.2f);
    }

    [Fact]
    public async Task CanInsertMosaicJobWithResourceLink()
    {
        await using var ctx = CreateContext();

        var user = new User { Id = Guid.NewGuid(), KeycloakId = "kc-789", Email = "job@test.com" };
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Filename = "source.jpg",
            StoragePath = "resources/source.jpg",
            Width = 1200,
            Height = 800,
            FileSizeBytes = 500_000
        };
        var job = new MosaicJob
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ResourceId = resource.Id,
            PxFormat = 94,
            TotalTiles = 64
        };

        ctx.Users.Add(user);
        ctx.Resources.Add(resource);
        ctx.MosaicJobs.Add(job);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.MosaicJobs
            .Include(j => j.Resource)
            .FirstAsync(j => j.Id == job.Id);
        loaded.Status.Should().Be(JobStatus.Queued);
        loaded.Resource.Width.Should().Be(1200);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Opalop.Infrastructure.Tests --filter "FullyQualifiedName~OpalopDbContextTests" -v n
```

Expected: FAIL — `OpalopDbContext` does not exist.

- [ ] **Step 3: Create OpalopDbContext**

```csharp
// src/Opalop.Infrastructure/Persistence/OpalopDbContext.cs
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
```

- [ ] **Step 4: Create entity configurations**

```csharp
// src/Opalop.Infrastructure/Persistence/Configurations/UserConfiguration.cs
namespace Opalop.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opalop.Domain.Entities;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(u => u.KeycloakId).IsUnique();
        builder.Property(u => u.KeycloakId).IsRequired();
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.TicketBalance).HasDefaultValue(100);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
    }
}
```

```csharp
// src/Opalop.Infrastructure/Persistence/Configurations/PhotoConfiguration.cs
namespace Opalop.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opalop.Domain.Entities;
using Opalop.Domain.ValueObjects;

public class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Filename).IsRequired();
        builder.Property(p => p.StoragePath).IsRequired();
        builder.Property(p => p.TilePath).IsRequired();
        builder.Property(p => p.Source).HasConversion<string>();
        builder.Property(p => p.IsActive).HasDefaultValue(true);

        builder.Property(p => p.Quadrants)
            .HasColumnType("jsonb");

        builder.HasIndex(p => p.UserId)
            .HasFilter("is_active = true");

        builder.HasIndex(p => new { p.UserId, p.TotalL })
            .HasFilter("is_active = true");

        builder.HasOne(p => p.User)
            .WithMany(u => u.Photos)
            .HasForeignKey(p => p.UserId);
    }
}
```

```csharp
// src/Opalop.Infrastructure/Persistence/Configurations/ResourceConfiguration.cs
namespace Opalop.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opalop.Domain.Entities;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.Filename).IsRequired();
        builder.Property(r => r.StoragePath).IsRequired();

        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.User)
            .WithMany(u => u.Resources)
            .HasForeignKey(r => r.UserId);
    }
}
```

```csharp
// src/Opalop.Infrastructure/Persistence/Configurations/MosaicJobConfiguration.cs
namespace Opalop.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opalop.Domain.Entities;

public class MosaicJobConfiguration : IEntityTypeConfiguration<MosaicJob>
{
    public void Configure(EntityTypeBuilder<MosaicJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(j => j.Status).HasConversion<string>();
        builder.Property(j => j.CompletedTiles).HasDefaultValue(0);

        builder.HasIndex(j => new { j.UserId, j.Status });
        builder.HasIndex(j => j.CreatedAt).IsDescending();

        builder.HasOne(j => j.User)
            .WithMany(u => u.MosaicJobs)
            .HasForeignKey(j => j.UserId);

        builder.HasOne(j => j.Resource)
            .WithMany(r => r.MosaicJobs)
            .HasForeignKey(j => j.ResourceId);
    }
}
```

```csharp
// src/Opalop.Infrastructure/Persistence/Configurations/SocialConnectionConfiguration.cs
namespace Opalop.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opalop.Domain.Entities;

public class SocialConnectionConfiguration : IEntityTypeConfiguration<SocialConnection>
{
    public void Configure(EntityTypeBuilder<SocialConnection> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Provider).HasConversion<string>();
        builder.Property(s => s.AccessToken).IsRequired();

        builder.HasIndex(s => new { s.UserId, s.Provider }).IsUnique();

        builder.HasOne(s => s.User)
            .WithMany(u => u.SocialConnections)
            .HasForeignKey(s => s.UserId);
    }
}
```

- [ ] **Step 5: Create DesignTimeDbContextFactory (for EF migrations)**

```csharp
// src/Opalop.Infrastructure/Persistence/DesignTimeDbContextFactory.cs
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
```

- [ ] **Step 6: Run integration tests**

```bash
dotnet test tests/Opalop.Infrastructure.Tests --filter "FullyQualifiedName~OpalopDbContextTests" -v n
```

Expected: 3 tests pass (Testcontainers starts a real PostgreSQL container).

- [ ] **Step 7: Commit**

```bash
git add src/Opalop.Infrastructure/Persistence/ tests/Opalop.Infrastructure.Tests/
git commit -m "feat: add EF Core DbContext with PostgreSQL entity configurations and integration tests"
```

---

### Task 5: Docker Compose & Infrastructure Setup

**Files:**
- Create: `docker-compose.yml`
- Create: `docker-compose.override.yml`
- Create: `infra/keycloak/realm-export.json`
- Create: `infra/minio/init-buckets.sh`

- [ ] **Step 1: Create docker-compose.yml**

```yaml
# docker-compose.yml
services:
  opalop-api:
    build:
      context: .
      dockerfile: src/Opalop.Api/Dockerfile
    ports:
      - "5000:8080"
    depends_on:
      redis:
        condition: service_started
      postgresql:
        condition: service_healthy
      minio:
        condition: service_started
      keycloak:
        condition: service_started
    environment:
      - ConnectionStrings__PostgreSQL=Host=postgresql;Database=opalop;Username=opalop;Password=dev_password
      - ConnectionStrings__Redis=redis:6379
      - MinIO__Endpoint=minio:9000
      - MinIO__AccessKey=minioadmin
      - MinIO__SecretKey=minioadmin
      - MinIO__UseSSL=false
      - Keycloak__Authority=http://keycloak:8080/realms/opalop
      - Keycloak__Audience=opalop-api

  opalop-worker:
    build:
      context: .
      dockerfile: src/Opalop.Worker/Dockerfile
    depends_on:
      redis:
        condition: service_started
      postgresql:
        condition: service_healthy
      minio:
        condition: service_started
    deploy:
      replicas: 2
    environment:
      - ConnectionStrings__PostgreSQL=Host=postgresql;Database=opalop;Username=opalop;Password=dev_password
      - ConnectionStrings__Redis=redis:6379
      - MinIO__Endpoint=minio:9000
      - MinIO__AccessKey=minioadmin
      - MinIO__SecretKey=minioadmin
      - MinIO__UseSSL=false
      - Worker__ConsumerGroup=workers
      - Worker__StreamKey=mosaic:tiles
      - Worker__ClaimTimeout=30000

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes --maxmemory 512mb --maxmemory-policy noeviction

  postgresql:
    image: postgres:16-alpine
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    environment:
      - POSTGRES_DB=opalop
      - POSTGRES_USER=opalop
      - POSTGRES_PASSWORD=dev_password
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U opalop -d opalop"]
      interval: 5s
      timeout: 5s
      retries: 5

  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    volumes:
      - minio-data:/data
    command: server /data --console-address ":9001"
    environment:
      - MINIO_ROOT_USER=minioadmin
      - MINIO_ROOT_PASSWORD=minioadmin

  minio-init:
    image: minio/mc:latest
    depends_on:
      - minio
    entrypoint: >
      /bin/sh -c "
      sleep 3;
      mc alias set local http://minio:9000 minioadmin minioadmin;
      mc mb --ignore-existing local/photos;
      mc mb --ignore-existing local/resources;
      mc mb --ignore-existing local/mosaics;
      echo 'Buckets created.';
      "

  keycloak:
    image: quay.io/keycloak/keycloak:24.0
    ports:
      - "8080:8080"
    volumes:
      - ./infra/keycloak/realm-export.json:/opt/keycloak/data/import/realm.json
    command: start-dev --import-realm
    environment:
      - KEYCLOAK_ADMIN=admin
      - KEYCLOAK_ADMIN_PASSWORD=admin
      - KC_HEALTH_ENABLED=true

volumes:
  redis-data:
  pgdata:
  minio-data:
```

- [ ] **Step 2: Create docker-compose.override.yml**

```yaml
# docker-compose.override.yml
# Dev-only overrides — mounts source for hot reload
services:
  opalop-api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
  opalop-worker:
    environment:
      - DOTNET_ENVIRONMENT=Development
```

- [ ] **Step 3: Create minimal Keycloak realm export**

```json
// infra/keycloak/realm-export.json
{
  "realm": "opalop",
  "enabled": true,
  "roles": {
    "realm": [
      { "name": "user", "composite": false },
      { "name": "admin", "composite": false }
    ]
  },
  "clients": [
    {
      "clientId": "opalop-api",
      "enabled": true,
      "publicClient": true,
      "directAccessGrantsEnabled": true,
      "redirectUris": ["http://localhost:5000/*"],
      "webOrigins": ["http://localhost:5000"],
      "defaultClientScopes": ["openid", "profile", "email"]
    }
  ],
  "users": [
    {
      "username": "testuser",
      "enabled": true,
      "email": "test@opalop.com",
      "emailVerified": true,
      "credentials": [{ "type": "password", "value": "test123", "temporary": false }],
      "realmRoles": ["user"]
    },
    {
      "username": "admin",
      "enabled": true,
      "email": "admin@opalop.com",
      "emailVerified": true,
      "credentials": [{ "type": "password", "value": "admin123", "temporary": false }],
      "realmRoles": ["admin", "user"]
    }
  ]
}
```

- [ ] **Step 4: Create MinIO init script**

```bash
#!/bin/sh
# infra/minio/init-buckets.sh
mc alias set local http://localhost:9000 minioadmin minioadmin
mc mb --ignore-existing local/photos
mc mb --ignore-existing local/resources
mc mb --ignore-existing local/mosaics
echo "Buckets created: photos, resources, mosaics"
```

- [ ] **Step 5: Test infrastructure starts**

```bash
docker compose up -d redis postgresql minio minio-init keycloak
docker compose ps
```

Expected: All services running. `minio-init` exits 0 after creating buckets.

- [ ] **Step 6: Commit**

```bash
git add docker-compose.yml docker-compose.override.yml infra/
git commit -m "feat: add Docker Compose with Redis, PostgreSQL, MinIO, and Keycloak"
```

---

### Task 6: API & Worker Minimal Program.cs with Health Checks

**Files:**
- Create: `src/Opalop.Api/Program.cs`
- Create: `src/Opalop.Api/appsettings.json`
- Create: `src/Opalop.Api/Dockerfile`
- Create: `src/Opalop.Worker/Program.cs`
- Create: `src/Opalop.Worker/Dockerfile`
- Create: `src/Opalop.Infrastructure/ServiceRegistration.cs`

- [ ] **Step 1: Create Infrastructure ServiceRegistration**

```csharp
// src/Opalop.Infrastructure/ServiceRegistration.cs
namespace Opalop.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Opalop.Infrastructure.Persistence;

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

        return services;
    }
}
```

Add the health check NuGet packages to Infrastructure.csproj:

```xml
<!-- Add to src/Opalop.Infrastructure/Opalop.Infrastructure.csproj ItemGroup -->
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.0.1" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.0.1" />
```

And add these to `Directory.Packages.props`:

```xml
<PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="9.0.1" />
<PackageVersion Include="AspNetCore.HealthChecks.Redis" Version="9.0.1" />
```

- [ ] **Step 2: Create Api appsettings.json**

```json
// src/Opalop.Api/appsettings.json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=opalop;Username=opalop;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "UseSSL": false
  },
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/opalop",
    "Audience": "opalop-api"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- [ ] **Step 3: Create Api Program.cs**

```csharp
// src/Opalop.Api/Program.cs
using Opalop.Infrastructure;
using Opalop.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = false; // dev only
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
```

- [ ] **Step 4: Create Worker Program.cs**

```csharp
// src/Opalop.Worker/Program.cs
using Opalop.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
```

- [ ] **Step 5: Create Api Dockerfile**

```dockerfile
# src/Opalop.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Opalop.Domain/Opalop.Domain.csproj src/Opalop.Domain/
COPY src/Opalop.Application/Opalop.Application.csproj src/Opalop.Application/
COPY src/Opalop.Infrastructure/Opalop.Infrastructure.csproj src/Opalop.Infrastructure/
COPY src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj src/Opalop.Mosaic.Engine/
COPY src/Opalop.Api/Opalop.Api.csproj src/Opalop.Api/
RUN dotnet restore src/Opalop.Api/Opalop.Api.csproj
COPY src/ src/
RUN dotnet publish src/Opalop.Api/Opalop.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Opalop.Api.dll"]
```

- [ ] **Step 6: Create Worker Dockerfile**

```dockerfile
# src/Opalop.Worker/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Opalop.Domain/Opalop.Domain.csproj src/Opalop.Domain/
COPY src/Opalop.Application/Opalop.Application.csproj src/Opalop.Application/
COPY src/Opalop.Infrastructure/Opalop.Infrastructure.csproj src/Opalop.Infrastructure/
COPY src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj src/Opalop.Mosaic.Engine/
COPY src/Opalop.Worker/Opalop.Worker.csproj src/Opalop.Worker/
RUN dotnet restore src/Opalop.Worker/Opalop.Worker.csproj
COPY src/ src/
RUN dotnet publish src/Opalop.Worker/Opalop.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Opalop.Worker.dll"]
```

- [ ] **Step 7: Build and verify**

```bash
dotnet build Opalop.sln
```

Expected: Build succeeded.

- [ ] **Step 8: Test health check with infrastructure running**

```bash
docker compose up -d redis postgresql minio minio-init keycloak
dotnet run --project src/Opalop.Api -- --urls "http://localhost:5000" &
sleep 5
curl http://localhost:5000/health
```

Expected: `Healthy` response.

- [ ] **Step 9: Commit**

```bash
git add src/Opalop.Api/ src/Opalop.Worker/ src/Opalop.Infrastructure/ServiceRegistration.cs Directory.Packages.props
git commit -m "feat: add Api and Worker Program.cs with health checks, Dockerfiles, and infrastructure registration"
```

---

### Task 7: EF Core Initial Migration

**Files:**
- Create: `src/Opalop.Infrastructure/Persistence/Migrations/` (auto-generated)

- [ ] **Step 1: Generate the initial migration**

```bash
dotnet ef migrations add InitialCreate -p src/Opalop.Infrastructure -s src/Opalop.Api -o Persistence/Migrations
```

Expected: Migration files created in `src/Opalop.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 2: Apply migration to running PostgreSQL**

```bash
docker compose up -d postgresql
dotnet ef database update -p src/Opalop.Infrastructure -s src/Opalop.Api
```

Expected: Database `opalop` has all 5 tables with correct snake_case naming.

- [ ] **Step 3: Verify table structure**

```bash
docker compose exec postgresql psql -U opalop -d opalop -c "\dt"
```

Expected output should show tables: `users`, `photos`, `resources`, `mosaic_jobs`, `social_connections`.

- [ ] **Step 4: Run all tests to verify nothing broke**

```bash
dotnet test Opalop.sln -v n
```

Expected: All tests pass (domain + infrastructure integration tests).

- [ ] **Step 5: Commit**

```bash
git add src/Opalop.Infrastructure/Persistence/Migrations/
git commit -m "feat: add EF Core initial migration for PostgreSQL schema"
```

---

### Task 8: Full Docker Compose Smoke Test

**Files:** No new files — verification only.

- [ ] **Step 1: Build all Docker images**

```bash
docker compose build
```

Expected: Both `opalop-api` and `opalop-worker` images build successfully.

- [ ] **Step 2: Start the full stack**

```bash
docker compose up -d
docker compose ps
```

Expected: All services running (api, worker ×2, redis, postgresql, minio, keycloak).

- [ ] **Step 3: Verify API health**

```bash
curl http://localhost:5000/health
```

Expected: `Healthy`

- [ ] **Step 4: Verify Swagger UI**

Open `http://localhost:5000/swagger` in browser. Expected: Swagger UI loads with health endpoint listed.

- [ ] **Step 5: Verify Keycloak**

Open `http://localhost:8080` in browser. Login with `admin`/`admin`. Navigate to realm `opalop`. Expected: realm exists with 2 users (testuser, admin).

- [ ] **Step 6: Verify MinIO**

Open `http://localhost:9001` in browser. Login with `minioadmin`/`minioadmin`. Expected: 3 buckets exist (photos, resources, mosaics).

- [ ] **Step 7: Stop and clean up**

```bash
docker compose down
```

- [ ] **Step 8: Commit (if any fixes were needed)**

```bash
git status
# If changes: git add -A && git commit -m "fix: Docker Compose smoke test fixes"
```

---

## Summary

| Task | What it produces | Depends on |
|------|-----------------|------------|
| 1 | Solution scaffold, 8 projects, builds | — |
| 2 | Domain entities, value objects, enums + tests | Task 1 |
| 3 | Application interfaces (IColorIndex, etc.) | Task 2 |
| 4 | EF Core DbContext + configurations + integration tests | Task 2, 3 |
| 5 | Docker Compose (Redis, PgSQL, MinIO, Keycloak) | — |
| 6 | Api & Worker Program.cs, Dockerfiles, health checks | Task 4, 5 |
| 7 | EF Core initial migration | Task 4, 6 |
| 8 | Full stack smoke test | All |
