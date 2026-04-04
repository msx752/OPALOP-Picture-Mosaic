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
