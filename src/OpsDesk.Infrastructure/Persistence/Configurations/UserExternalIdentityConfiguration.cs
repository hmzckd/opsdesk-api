using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class UserExternalIdentityConfiguration : IEntityTypeConfiguration<UserExternalIdentity>
{
    // Keeps provider subjects unique and removes identity links only when their user is removed.
    public void Configure(EntityTypeBuilder<UserExternalIdentity> builder)
    {
        builder.ToTable("user_external_identities");
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(identity => identity.UserId).HasColumnName("user_id");
        builder.Property(identity => identity.Issuer).HasColumnName("issuer").HasMaxLength(512).IsRequired();
        builder.Property(identity => identity.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
        builder.Property(identity => identity.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(identity => new { identity.Issuer, identity.Subject }).IsUnique()
            .HasDatabaseName("ux_user_external_identities_issuer_subject");
        builder.HasOne<User>().WithMany().HasForeignKey(identity => identity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
