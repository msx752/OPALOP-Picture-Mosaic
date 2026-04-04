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
        builder.HasOne(s => s.User).WithMany(u => u.SocialConnections).HasForeignKey(s => s.UserId);
    }
}
