using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    // Stores invitation history and permits only one non-revoked invitation per address.
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("user_invitations", table =>
        {
            table.HasCheckConstraint("ck_user_invitations_expiry", "expires_at_utc > created_at_utc");
            table.HasCheckConstraint("ck_user_invitations_role", "role IN ('Customer', 'Agent')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InvitedById).HasColumnName("invited_by_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(x => x.AcceptedAtUtc).HasColumnName("accepted_at_utc");
        builder.Property(x => x.AcceptedUserId).HasColumnName("accepted_user_id");
        builder.HasIndex(x => x.Email).IsUnique()
            .HasFilter("revoked_at_utc IS NULL AND accepted_at_utc IS NULL")
            .HasDatabaseName("ux_user_invitations_pending_email");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AcceptedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
