namespace Opalop.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opalop.Domain.Entities;
using Opalop.Domain.ValueObjects;

public class MosaicJobConfiguration : IEntityTypeConfiguration<MosaicJob>
{
    public void Configure(EntityTypeBuilder<MosaicJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(j => j.Status).HasConversion<string>();
        builder.Property(j => j.PxFormat).HasConversion(
            v => v.Size,
            v => PixFormat.From(v));
        builder.Property(j => j.CompletedTiles).HasDefaultValue(0);
        builder.HasIndex(j => new { j.UserId, j.Status });
        builder.HasIndex(j => j.CreatedAt).IsDescending();
        builder.HasOne(j => j.User).WithMany(u => u.MosaicJobs).HasForeignKey(j => j.UserId);
        builder.HasOne(j => j.Resource).WithMany(r => r.MosaicJobs).HasForeignKey(j => j.ResourceId);
    }
}
