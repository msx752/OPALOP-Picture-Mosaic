namespace Opalop.Infrastructure.Persistence.Configurations;

using System.Text.Json;
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
        builder.Property(p => p.Source).HasConversion<string>();
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.Property(p => p.Quadrants)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<QuadrantLab>>(v, JsonSerializerOptions.Default) ?? new List<QuadrantLab>());
        builder.HasIndex(p => p.UserId).HasFilter("is_active = true");
        builder.HasIndex(p => new { p.UserId, p.TotalL }).HasFilter("is_active = true");
        builder.HasOne(p => p.User).WithMany(u => u.Photos).HasForeignKey(p => p.UserId);
    }
}
