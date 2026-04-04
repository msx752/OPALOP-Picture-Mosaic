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
        builder.HasOne(r => r.User).WithMany(u => u.Resources).HasForeignKey(r => r.UserId);
    }
}
