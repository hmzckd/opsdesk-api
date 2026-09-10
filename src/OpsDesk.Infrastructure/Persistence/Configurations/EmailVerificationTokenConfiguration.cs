using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class EmailVerificationTokenConfiguration :
    IEntityTypeConfiguration<EmailVerificationToken>
{
    /// <summary>
    /// Maps email verification tokens and their User relationship to PostgreSQL.
    /// </summary>
    public void Configure(
        EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable(
            "email_verification_tokens",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_email_verification_tokens_expiry",
                "expires_at_utc > created_at_utc"));

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(token => token.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(token => token.UserId)
            .IsUnique()
            .HasDatabaseName(
                "ux_email_verification_tokens_user_id");

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName(
                "ux_email_verification_tokens_token_hash");

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<EmailVerificationToken>(
                token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_email_verification_tokens_user");
    }
}
