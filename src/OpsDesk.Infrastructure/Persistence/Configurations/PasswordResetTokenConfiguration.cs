using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    // Keeps one reset credential per account and never persists a raw token.
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens", table => table.HasCheckConstraint(
            "ck_password_reset_tokens_expiry", "expires_at_utc > created_at_utc"));
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(token => token.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(token => token.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(token => token.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.HasIndex(token => token.UserId).IsUnique();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
